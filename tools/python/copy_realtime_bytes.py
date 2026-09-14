"""out/masterdata.bytes を realtime_server 側へ配置する(clientと同一内容)。

配置先ディレクトリは masterdata.bytes 専用なので、コピー前にクリーンアップする。
"""

from __future__ import annotations

import sys

from common import REPO_ROOT, copy_dir_contents, load_config


def main() -> None:
    config = load_config()
    bytes_path = REPO_ROOT / config["csharp_converter"]["output_path"]

    if not bytes_path.exists():
        raise FileNotFoundError(
            f"{bytes_path} が見つかりません(先にcsharp_converterを実行してください)"
        )

    dest_dir = (REPO_ROOT / config["copy_destinations"]["realtime_bytes_dest_dir"]).resolve()
    copied = copy_dir_contents(bytes_path.parent, dest_dir, bytes_path.name)
    print(f"copied: {bytes_path} -> {copied[0]}")


if __name__ == "__main__":
    try:
        main()
    except FileNotFoundError as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)
