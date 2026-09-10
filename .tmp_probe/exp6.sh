#!/bin/bash
B="/c/Program Files (x86)/Steam/steamapps/common/Territory/BepInEx"
PY="C:/Users/c/.workbuddy/binaries/python/versions/3.13.12/python.exe"
for i in $(seq 1 120); do
  ST=$(curl -s -m 3 http://localhost:8765/api/editor/state 2>/dev/null)
  if echo "$ST" | grep -q '"inSave":true'; then echo "[$i] 进存档，等 12 秒"; sleep 12; break; fi
  sleep 5
done
# 记录召唤前动物数
curl -s -m 60 http://localhost:8765/api/editor/entities -o .tmp_probe/before.json
BEFORE=$("$PY" -c "
import json
d=json.load(open(r'.tmp_probe/before.json',encoding='utf-8'))
print(sum(1 for e in d if e.get('className')=='Animal'))")
echo "召唤前动物 $BEFORE 只"
# 召唤 1 只鸡
curl -s -m 15 -X POST -H "Content-Type: application/json" -d '{"stuffId": 501001, "count": 1}' http://localhost:8765/api/editor/animal/spawn -w "http=%{http_code}"
echo ""
# 重扫
curl -s -m 60 -X POST -H "Content-Type: application/json" -d '{}' http://localhost:8765/api/editor/scan -o /dev/null -w "scan http=%{http_code}"
echo ""
curl -s -m 30 http://localhost:8765/api/editor/entities -o .tmp_probe/after.json
# 找新鸡并定位读真实坐标
"$PY" -c "
import json, subprocess
b=json.load(open(r'.tmp_probe/before.json',encoding='utf-8'))
a=json.load(open(r'.tmp_probe/after.json',encoding='utf-8'))
bg={e['ptrHash'] for e in b if e.get('className')=='Animal'}
new=[e for e in a if e.get('className')=='Animal' and e['ptrHash'] not in bg]
print('新增动物', len(new), '只')
for e in new:
    r=subprocess.run(['curl','-s','-m','10','-X','POST','-H','Content-Type: application/json',
        '-d',json.dumps({'ptrHash':e['ptrHash']}),'http://localhost:8765/api/editor/locate'],capture_output=True,text=True)
    try:
        pos=json.loads(r.stdout)
        print(f\"  新动物 {e.get('name')} guid={e['guid']} 位置 x={pos.get('x')} y={pos.get('y')}\")
    except: print('  定位失败:', r.stdout[:80])"
echo "=== 实验结束 ==="
