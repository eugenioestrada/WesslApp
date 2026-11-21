import argparse
import hashlib
from pathlib import Path
import re


PACKAGE_ID = "eugenioestrada.Wessl"
REPOSITORY = "eugenioestrada/WesslApp"
MANIFEST_VERSION = "1.10.0"
RELEASE_VERSION = re.compile(r"v(?P<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?)\Z")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as file:
        for chunk in iter(lambda: file.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def write_manifests(tag: str, assets_directory: Path, output_directory: Path) -> None:
    match = RELEASE_VERSION.fullmatch(tag)
    if match is None:
        raise ValueError("Release tag must use vMAJOR.MINOR.PATCH with an optional prerelease suffix.")
    version = match.group("version")

    assets = {
        "win-x64": assets_directory / "wessl-win-x64.zip",
        "win-arm64": assets_directory / "wessl-win-arm64.zip",
    }
    for asset in assets.values():
        if not asset.is_file():
            raise FileNotFoundError(f"Release archive not found: {asset}")

    release_url = f"https://github.com/{REPOSITORY}/releases/download/{tag}"
    installer_entries = []
    for architecture, asset in assets.items():
        installer_entries.extend(
            [
                f"- Architecture: {architecture.split('-', maxsplit=1)[1]}",
                "  InstallerType: zip",
                f"  InstallerUrl: {release_url}/{asset.name}",
                f"  InstallerSha256: {sha256(asset)}",
                "  NestedInstallerType: portable",
                "  NestedInstallerFiles:",
                "  - RelativeFilePath: wessl.exe",
                "    PortableCommandAlias: wessl",
            ]
        )

    manifests = {
        f"{PACKAGE_ID}.yaml": (
            f"# yaml-language-server: $schema=https://aka.ms/winget-manifest.version.{MANIFEST_VERSION}.schema.json\n"
            f"PackageIdentifier: {PACKAGE_ID}\n"
            f"PackageVersion: {version}\n"
            "DefaultLocale: en-US\n"
            "ManifestType: version\n"
            f"ManifestVersion: {MANIFEST_VERSION}\n"
        ),
        f"{PACKAGE_ID}.installer.yaml": (
            f"# yaml-language-server: $schema=https://aka.ms/winget-manifest.installer.{MANIFEST_VERSION}.schema.json\n"
            f"PackageIdentifier: {PACKAGE_ID}\n"
            f"PackageVersion: {version}\n"
            "Installers:\n"
            + "\n".join(f"  {line}" if line.startswith("- ") else f"  {line}" for line in installer_entries)
            + "\nManifestType: installer\n"
            f"ManifestVersion: {MANIFEST_VERSION}\n"
        ),
        f"{PACKAGE_ID}.locale.en-US.yaml": (
            f"# yaml-language-server: $schema=https://aka.ms/winget-manifest.defaultLocale.{MANIFEST_VERSION}.schema.json\n"
            f"PackageIdentifier: {PACKAGE_ID}\n"
            f"PackageVersion: {version}\n"
            "PackageLocale: en-US\n"
            "Publisher: Wessl contributors\n"
            "PublisherUrl: https://github.com/eugenioestrada/WesslApp\n"
            "PackageName: Wessl\n"
            "PackageUrl: https://github.com/eugenioestrada/WesslApp\n"
            "License: MIT\n"
            "LicenseUrl: https://github.com/eugenioestrada/WesslApp/blob/main/LICENSE\n"
            "ShortDescription: Native Windows system-tray app for managing WSL Containers\n"
            "Tags:\n"
            "- WSL\n"
            "- Containers\n"
            "- Windows\n"
            f"ReleaseNotesUrl: {release_url}\n"
            "ManifestType: defaultLocale\n"
            f"ManifestVersion: {MANIFEST_VERSION}\n"
        ),
    }

    output_directory.mkdir(parents=True, exist_ok=True)
    for filename, contents in manifests.items():
        with (output_directory / filename).open("w", encoding="utf-8", newline="\n") as manifest:
            manifest.write(contents)


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate winget manifests for a Wessl release.")
    parser.add_argument("--tag", required=True)
    parser.add_argument("--assets-directory", required=True, type=Path)
    parser.add_argument("--output-directory", required=True, type=Path)
    args = parser.parse_args()
    write_manifests(args.tag, args.assets_directory, args.output_directory)


if __name__ == "__main__":
    main()
