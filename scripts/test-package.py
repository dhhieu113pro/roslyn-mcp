#!/usr/bin/env python3
"""Validate the NuGet package and exercise it through dnx over MCP stdio."""

from __future__ import annotations

import json
import os
from pathlib import Path
import queue
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import xml.etree.ElementTree as element_tree
import zipfile


PACKAGE_ID = "RoslynMcp.Dnx"


def fail(message: str) -> None:
    raise RuntimeError(message)


def find_package(package_directory: Path, version: str) -> Path:
    expected_name = f"{PACKAGE_ID}.{version}.nupkg"
    packages = [
        path
        for path in package_directory.glob("*.nupkg")
        if not path.name.endswith(".snupkg")
    ]
    for package in packages:
        if package.name.casefold() == expected_name.casefold():
            return package

    fail(f"Expected {expected_name}; found: {', '.join(path.name for path in packages) or 'nothing'}")


def required_text(parent: element_tree.Element, path: str, namespace: dict[str, str]) -> str:
    element = parent.find(path, namespace)
    if element is None or not element.text:
        fail(f"Package metadata is missing {path}")
    return element.text


def validate_package(package: Path, version: str) -> None:
    symbol_package = package.with_suffix(".snupkg")
    if not symbol_package.is_file():
        fail(f"Symbol package is missing: {symbol_package.name}")

    with zipfile.ZipFile(package) as archive:
        names = set(archive.namelist())
        required_files = {
            ".mcp/server.json",
            "README.md",
            "THIRD_PARTY_NOTICES.md",
            "LICENSE",
            "tools/net10.0/any/DotnetToolSettings.xml",
            "tools/net10.0/any/RoslynMcp.dll",
            "tools/net10.0/any/RoslynMcp.runtimeconfig.json",
        }
        missing_files = sorted(required_files - names)
        if missing_files:
            fail(f"Package is missing: {', '.join(missing_files)}")

        tool_settings = element_tree.fromstring(
            archive.read("tools/net10.0/any/DotnetToolSettings.xml")
        )
        tool_commands = tool_settings.findall(".//Command")
        if len(tool_commands) != 1 or tool_commands[0].get("Name") != "RoslynMcp":
            fail("Package must expose exactly one RoslynMcp tool command")

        runtime_configuration = json.loads(
            archive.read("tools/net10.0/any/RoslynMcp.runtimeconfig.json")
        )
        roll_forward = runtime_configuration.get("runtimeOptions", {}).get("rollForward")
        if roll_forward != "Major":
            fail(f"Package runtime roll-forward must be Major; found {roll_forward}")

        nuspec_names = [name for name in names if name.endswith(".nuspec")]
        if len(nuspec_names) != 1:
            fail(f"Expected one nuspec; found {len(nuspec_names)}")

        nuspec = element_tree.fromstring(archive.read(nuspec_names[0]))
        namespace_uri = nuspec.tag.partition("}")[0].removeprefix("{")
        namespace = {"n": namespace_uri} if namespace_uri else {}
        prefix = "n:" if namespace_uri else ""
        metadata = nuspec.find(f"{prefix}metadata", namespace)
        if metadata is None:
            fail("Package nuspec has no metadata element")

        actual_id = required_text(metadata, f"{prefix}id", namespace)
        actual_version = required_text(metadata, f"{prefix}version", namespace)
        if actual_id != PACKAGE_ID:
            fail(f"Expected package ID {PACKAGE_ID}; found {actual_id}")
        if actual_version != version:
            fail(f"Expected package version {version}; found {actual_version}")

        license_element = metadata.find(f"{prefix}license", namespace)
        if license_element is None or license_element.get("type") != "expression" or license_element.text != "MIT":
            fail("Package license must be the MIT expression")

        repository = metadata.find(f"{prefix}repository", namespace)
        if repository is None or repository.get("url") != "https://github.com/dhhieu113pro/roslyn-mcp":
            fail("Package repository metadata is incorrect")

        package_types = {
            item.get("name")
            for item in metadata.findall(f"{prefix}packageTypes/{prefix}packageType", namespace)
        }
        if not {"DotnetTool", "McpServer"}.issubset(package_types):
            fail(f"Expected DotnetTool and McpServer package types; found {sorted(package_types)}")

        manifest = json.loads(archive.read(".mcp/server.json"))
        if manifest.get("version") != version:
            fail("MCP manifest top-level version does not match the package")
        packages = manifest.get("packages")
        if not isinstance(packages, list) or not packages:
            fail("MCP manifest has no package declaration")
        declaration = packages[0]
        if declaration.get("identifier") != PACKAGE_ID or declaration.get("version") != version:
            fail("MCP manifest package identity does not match the NuGet package")
        if declaration.get("transport", {}).get("type") != "stdio":
            fail("MCP manifest must declare stdio transport")


def send_message(process: subprocess.Popen[str], message: dict[str, object]) -> None:
    if process.stdin is None:
        fail("dnx stdin is unavailable")
    process.stdin.write(json.dumps(message, separators=(",", ":")) + "\n")
    process.stdin.flush()


def wait_for_response(
    process: subprocess.Popen[str],
    messages: queue.Queue[dict[str, object]],
    response_id: int,
    stderr_lines: list[str],
    timeout_seconds: float = 90,
) -> dict[str, object]:
    deadline = time.monotonic() + timeout_seconds
    while True:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            break
        try:
            message = messages.get(timeout=min(0.5, remaining))
        except queue.Empty:
            if process.poll() is not None:
                fail(f"dnx exited with code {process.returncode}: {''.join(stderr_lines)}")
            continue

        if "_invalid_stdout" in message:
            fail(f"dnx wrote non-JSON data to stdout: {message['_invalid_stdout']}")
        if message.get("id") == response_id:
            return message

    fail(f"Timed out waiting for MCP response {response_id}: {''.join(stderr_lines)}")


def smoke_test(package_directory: Path, version: str) -> None:
    dnx = shutil.which("dnx")
    if dnx is None:
        fail("dnx was not found; install the .NET 10 SDK")

    messages: queue.Queue[dict[str, object]] = queue.Queue()
    stderr_lines: list[str] = []
    with tempfile.TemporaryDirectory(prefix="roslyn-mcp-dnx-") as temporary_directory:
        environment = os.environ.copy()
        environment.update(
            {
                "DOTNET_CLI_HOME": str(Path(temporary_directory) / "dotnet-home"),
                "DOTNET_NOLOGO": "1",
                "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
                "NUGET_PACKAGES": str(Path(temporary_directory) / "packages"),
            }
        )
        command = [
            dnx,
            f"{PACKAGE_ID}@{version}",
            "--source",
            str(package_directory.resolve()),
            "--verbosity",
            "quiet",
            "--yes",
        ]
        process = subprocess.Popen(
            command,
            cwd=Path(__file__).resolve().parent.parent,
            env=environment,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
        )

        def read_stdout() -> None:
            assert process.stdout is not None
            for line in process.stdout:
                stripped = line.strip()
                if not stripped:
                    continue
                try:
                    messages.put(json.loads(stripped))
                except json.JSONDecodeError:
                    messages.put({"_invalid_stdout": stripped})

        def read_stderr() -> None:
            assert process.stderr is not None
            stderr_lines.extend(process.stderr.readlines())

        threading.Thread(target=read_stdout, daemon=True).start()
        threading.Thread(target=read_stderr, daemon=True).start()

        try:
            send_message(
                process,
                {
                    "jsonrpc": "2.0",
                    "id": 1,
                    "method": "initialize",
                    "params": {
                        "protocolVersion": "2025-06-18",
                        "capabilities": {},
                        "clientInfo": {"name": "package-smoke-test", "version": "1.0"},
                    },
                },
            )
            initialize = wait_for_response(process, messages, 1, stderr_lines)
            if "error" in initialize:
                fail(f"MCP initialization failed: {initialize['error']}")
            server_version = initialize.get("result", {}).get("serverInfo", {}).get("version")  # type: ignore[union-attr]
            if server_version != version:
                fail(f"Server advertised version {server_version}; expected {version}")

            send_message(process, {"jsonrpc": "2.0", "method": "notifications/initialized"})
            send_message(process, {"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}})
            tool_list = wait_for_response(process, messages, 2, stderr_lines)
            tools = tool_list.get("result", {}).get("tools", [])  # type: ignore[union-attr]
            tool_names = {tool.get("name") for tool in tools if isinstance(tool, dict)}
            if "diagnose" not in tool_names:
                fail("Packaged server did not advertise the diagnose tool")

            send_message(
                process,
                {
                    "jsonrpc": "2.0",
                    "id": 3,
                    "method": "tools/call",
                    "params": {"name": "diagnose", "arguments": {"path": ""}},
                },
            )
            diagnosis = wait_for_response(process, messages, 3, stderr_lines)
            if "error" in diagnosis or diagnosis.get("result", {}).get("isError") is True:  # type: ignore[union-attr]
                fail(f"Packaged diagnose call failed: {diagnosis}")
        finally:
            if process.stdin is not None:
                process.stdin.close()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: test-package.py <package-directory> <version>", file=sys.stderr)
        return 2

    package_directory = Path(sys.argv[1]).resolve()
    version = sys.argv[2]
    try:
        package = find_package(package_directory, version)
        validate_package(package, version)
        smoke_test(package_directory, version)
    except (OSError, RuntimeError, subprocess.SubprocessError, zipfile.BadZipFile) as exception:
        print(f"Package verification failed: {exception}", file=sys.stderr)
        return 1

    print(f"Validated and exercised {package.name} through dnx.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
