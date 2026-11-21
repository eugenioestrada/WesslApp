import argparse
from pathlib import Path
import zipfile


def create_archive(publish_directory: Path, output: Path) -> None:
    source = publish_directory.resolve(strict=True)
    if not source.is_dir():
        raise ValueError(f"Publish directory is not a directory: {source}")

    entries = list(source.rglob("*"))
    if any(entry.is_symlink() for entry in entries):
        raise ValueError("Publish output must not contain symbolic links.")

    files = sorted(
        (entry for entry in entries if entry.is_file()),
        key=lambda entry: entry.relative_to(source).as_posix().casefold(),
    )
    if not files or not any(
        file.relative_to(source).as_posix().casefold() == "wessl.exe" for file in files
    ):
        raise ValueError("Publish output must contain wessl.exe at its root and other app files.")

    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w") as archive:
        for file in files:
            relative_path = file.relative_to(source).as_posix()
            info = zipfile.ZipInfo(relative_path, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 0
            info.external_attr = 0
            archive.writestr(info, file.read_bytes(), compresslevel=9)

    with zipfile.ZipFile(output) as archive:
        if archive.testzip() is not None:
            raise ValueError(f"Archive verification failed: {output}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Create a deterministic Wessl release archive.")
    parser.add_argument("--publish-directory", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    create_archive(args.publish_directory, args.output)


if __name__ == "__main__":
    main()
