#!/usr/bin/env python3
"""Run coverage, skill validation, packaging, and MCP protocol verification."""

from pathlib import Path
import subprocess
import sys


def run(arguments: list[str], repository: Path) -> None:
    print(f"\n> {' '.join(arguments)}", flush=True)
    subprocess.run(arguments, cwd=repository, check=True)


def main() -> int:
    repository = Path(__file__).resolve().parent.parent
    try:
        run(["dotnet", "test", "RoslynMcp.slnx", "--configuration", "Release"], repository)
        run([sys.executable, "scripts/validate-skills.py"], repository)
    except subprocess.CalledProcessError as exception:
        return exception.returncode

    print("\nAll code, coverage, skill, and MCP protocol checks passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
