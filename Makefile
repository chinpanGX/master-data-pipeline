# よく使う生成の流れを1コマンドにまとめる入口。中身は run.sh / build.sh を
# 決まった順に呼ぶだけで、処理そのものは持たない。
# 別のリポジトリからは `make -C <このリポジトリのパス> generate` のように呼ぶ。
#
# 依存関係で順序を表しているため、1回の make の中で同じ処理は1度しか流れない
# (例: `make server client` でも check は1回)。順序が崩れないよう並列実行は禁止する。

## Usage: make <target>
##
##   check     normalize-csv -> resolve-enums -> validate (生成物は変えない)
##   server    check -> server
##   client    check -> client
##   realtime  check -> client -> realtime
##   generate  check -> server -> client -> realtime
##             手元のCSVから全部の配置先を作り直す
##   download  Google スプレッドシート -> csv/*.csv
##             (取り込んでから生成するなら: make download generate)
##   tools     ./build.sh tools (ツール本体を変えたとき)
##   help      この一覧
##
## 途中のコマンドが失敗した時点で止まります。個別のコマンドは ./run.sh を参照。
## config.yaml で空文字にした配置先はスキップされます。
## 環境変数 PYTHON はそのまま run.sh へ渡ります。

# help の文言は上の "## " 行から表示する。Windows の make はレシピのコマンドラインを
# ANSIコードページ経由で渡すため、echo に日本語を直接書くと文字化けする。
SELF  := $(lastword $(MAKEFILE_LIST))
RUN   := bash ./run.sh
BUILD := bash ./build.sh

.DEFAULT_GOAL := help
.NOTPARALLEL:
.PHONY: help check server client realtime generate download tools

help:
	@sed -n 's/^## \{0,1\}//p' $(SELF)

check:
	$(RUN) normalize-csv
	$(RUN) resolve-enums
	$(RUN) validate

server: check
	$(RUN) server

client: check
	$(RUN) client

realtime: client
	$(RUN) realtime

generate: check server client realtime

download:
	$(RUN) download

tools:
	$(BUILD) tools
