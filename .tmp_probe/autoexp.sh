#!/bin/bash
# 等游戏重启并进入存档（inSave=true），然后自动实验
for i in $(seq 1 120); do
  ST=$(curl -s -m 3 http://localhost:8765/api/editor/state 2>/dev/null)
  if echo "$ST" | grep -q '"inSave":true'; then
    echo "[$i] 游戏已进入存档，等 8 秒稳定..."
    sleep 8
    break
  fi
  sleep 5
done
B="/c/Program Files (x86)/Steam/steamapps/common/Territory/BepInEx"
MARK=$(wc -l < "$B/LogOutput.log")
echo "日志标记: $MARK"
# 找一只动物
curl -s -m 60 http://localhost:8765/api/editor/entities -o .tmp_probe/e2.json
T=$("C:/Users/c/.workbuddy/binaries/python/versions/3.13.12/python.exe" -c "
import json
d=json.load(open(r'.tmp_probe/e2.json',encoding='utf-8'))
ani=[e for e in d if e.get('className')=='Animal']
print(ani[0]['ptrHash'] if ani else 0)")
echo "实验目标: $T"
if [ "$T" = "0" ] || [ -z "$T" ]; then echo "没有动物可测"; exit 1; fi
# 删 1 只
curl -s -m 15 -X POST -H "Content-Type: application/json" -d "{\"ptrHashes\": [$T]}" http://localhost:8765/api/editor/animal/batch -w "\nhttp=%{http_code}\n"
sleep 3
echo "=== 进程 ==="
tasklist 2>/dev/null | grep -ic Territory
echo "=== 本轮 AnimalService 日志 ==="
tail -n +$MARK "$B/LogOutput.log" | grep -a "AnimalService" | tail -15
echo "=== 实验结束 ==="
