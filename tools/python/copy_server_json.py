"""out/server/*.json を既存Rust APIサーバー側へ配置する。

配置先ディレクトリは生成物専用なので、コピー前にクリーンアップする。
"""

from __future__ import annotations

import sys

from common import REPO_ROOT, copy_dir_contents, load_config, resolve_dest_dir


def main() -> None:
    config = load_config()
    src_dir = REPO_ROOT / config["server_codegen"]["output_json_dir"]

    dest_dir = resolve_dest_dir(config["copy_destinations"], "server_json_dest_dir")
    if dest_dir is None:
        print("skip: server_json_dest_dir が未設定のためコピーをスキップしました")
        return

    if not src_dir.exists():
        raise FileNotFoundError(
            f"{src_dir} が見つかりません(先にserver_codegenを実行してください)"
        )

    copied = copy_dir_contents(src_dir, dest_dir, "*.json")
    print(f"copied {len(copied)} files: {src_dir} -> {dest_dir}")


if __name__ == "__main__":
    try:
        main()
    except FileNotFoundError as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)
