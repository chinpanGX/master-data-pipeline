"""out/masterdata.bytes を client 側の StreamingAssets へ配置する。

配置先ディレクトリは masterdata.bytes 専用なので、コピー前にクリーンアップする。
"""

from __future__ import annotations

import sys

from common import REPO_ROOT, copy_dir_contents, load_config, resolve_dest_dir


def main() -> None:
    config = load_config()
    bytes_path = REPO_ROOT / config["csharp_converter"]["output_path"]

    dest_dir = resolve_dest_dir(config["copy_destinations"], "client_bytes_dest_dir")
    if dest_dir is None:
        print("skip: client_bytes_dest_dir が未設定のためコピーをスキップしました")
        return

    if not bytes_path.exists():
        raise FileNotFoundError(
            f"{bytes_path} が見つかりません(先にcsharp_converterを実行してください)"
        )

    copied = copy_dir_contents(bytes_path.parent, dest_dir, bytes_path.name, preserve_meta=True)
    print(f"copied: {bytes_path} -> {copied[0]}")


if __name__ == "__main__":
    try:
        main()
    except FileNotFoundError as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)
