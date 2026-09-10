#!/bin/bash
B="/c/Program Files (x86)/Steam/steamapps/common/Territory/BepInEx"
# 等游戏重启进存档（新 DLL 02:07 之后启动才算）
for i in $(seq 1 120); do
  ST=$(curl -s -m 3 http://localhost:8765/api/editor/state 2>/dev/null)
  if echo "$ST" | grep -q '"inSave":true'; then echo "[$i] 进存档，等 12 秒"; sleep 12; break; fi
  sleep 5
done
# 实验召唤 1 只鸡
MARK=$(wc -l < "$B/LogOutput.log")
curl -s -m 15 -X POST -H "Content-Type: application/json" -d '{"stuffId": 501001, "count": 1}' http://localhost:8765/api/editor/animal/spawn -w "http=%{http_code}"
echo ""
sleep 2
echo "=== 本轮日志 ==="
tail -n +$MARK "$B/LogOutput.log" | grep -a "AnimalService\|CameraSetTo" | tail -5
echo "=== 重扫确认 ==="
curl -s -m 60 -X POST -H "Content-Type: application/json" -d '{}' http://localhost:8765/api/editor/scan -o /dev/null -w "scan http=%{http_code}"
echo ""
curl -s -m 30 http://localhost:8765/api/editor/entities -o .tmp_probe/e6.json
"C:/Users/c/.workbuddy/binaries/python/versions/3.13.12/python.exe" -c "
import json, collections
d=json.load(open(r'.tmp_probe/e6.json',encoding='utf-8'))
ani=[e for e in d if e.get('className')=='Animal']
print('动物总数', len(ani))
print(collections.Counter(e.get('name') for e in ani).most_common(6))"
echo "=== 实验结束 ==="
