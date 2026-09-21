"""out/generated_csharp/MasterDataLoader.cs と AesCrypto.cs を realtime_server 側へ
コピーする(Unity/MagicOnionで完全に共通の生成物。AesCryptoはMasterDataLoaderが復号に
使う実装で、csharp_converter側の暗号化とも同一実装を共有する)。

コピー先ディレクトリ(Domain.MasterData/)には Models/ Enums/ など他のファイルも
同居するため、ディレクトリ全体はクリーンアップせず、このファイルたちだけを上書きする。
"""

from __future__ import annotations

import sys

from common import REPO_ROOT, copy_file, load_config, resolve_dest_dir

LOADER_FILES = ("MasterDataLoader.cs", "AesCrypto.cs")


def main() -> None:
    config = load_config()
    generated_dir = REPO_ROOT / config["csharp_codegen"]["output_dir"]
    dest = config["copy_destinations"]

    dest_dir = resolve_dest_dir(dest, "realtime_loader_dest_dir")
    if dest_dir is None:
        print("skip: realtime_loader_dest_dir が未設定のためコピーをスキップしました")
        return

    for filename in LOADER_FILES:
        src = generated_dir / filename
        if not src.exists():
            raise FileNotFoundError(
                f"{src} が見つかりません(先にcsharp-codegenを実行してください)"
            )
        copied = copy_file(src, dest_dir)
        print(f"copied: {src} -> {copied}")


if __name__ == "__main__":
    try:
        main()
    except FileNotFoundError as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)
