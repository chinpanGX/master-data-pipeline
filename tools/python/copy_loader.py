"""out/generated_csharp/MasterDataLoader.cs と AesCrypto.cs を client 側・
realtime_server 側の両方へコピーする(Unity/MagicOnionで完全に共通の生成物。
AesCryptoはMasterDataLoaderが復号に使う実装で、csharp_converter側の暗号化とも
同一実装を共有する)。

コピー先ディレクトリ(Domain.MasterData/)には Models/ Enums/ など他のファイルも
同居するため、ディレクトリ全体はクリーンアップせず、このファイルたちだけを上書きする。
"""

from __future__ import annotations

import sys

from common import REPO_ROOT, copy_file, load_config

LOADER_FILES = ("MasterDataLoader.cs", "AesCrypto.cs")


def main() -> None:
    config = load_config()
    generated_dir = REPO_ROOT / config["csharp_codegen"]["output_dir"]
    dest = config["copy_destinations"]

    srcs = []
    for filename in LOADER_FILES:
        src = generated_dir / filename
        if not src.exists():
            raise FileNotFoundError(
                f"{src} が見つかりません(先にcsharp-codegenを実行してください)"
            )
        srcs.append(src)

    for key in ("client_loader_dest_dir", "realtime_loader_dest_dir"):
        dest_dir = (REPO_ROOT / dest[key]).resolve()
        for src in srcs:
            copied = copy_file(src, dest_dir)
            print(f"copied: {src} -> {copied}")


if __name__ == "__main__":
    try:
        main()
    except FileNotFoundError as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)
