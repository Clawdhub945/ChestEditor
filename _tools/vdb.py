import io, json, re, sys

DB = r'C:\AI\游戏向量库\code_meta.json'

def load():
    # 83MB 数组，一次性载入（约 2-3 秒）
    return json.load(io.open(DB, encoding='utf-8', errors='ignore'))

def find(db, pattern, limit=10, show=True, max_len=4000):
    """按文件名/内容正则查找伪代码条目"""
    rx = re.compile(pattern, re.IGNORECASE)
    out = []
    for item in db:
        fname = item.get('file', '')
        if rx.search(fname):
            out.append(item)
            if show:
                c = item['content']
                print('=' * 20, fname.split('\\')[-1], f'({len(c)} chars)')
                print(c[:max_len])
                print()
            if len(out) >= limit:
                break
    if not show:
        return [i['file'].split('\\')[-1] for i in out]
    return out

def grep(db, pattern, limit=20, ctx=0):
    """在全部伪代码内容里 grep，打印文件名与命中行"""
    rx = re.compile(pattern)
    hits = 0
    for item in db:
        for line in item['content'].split('\n'):
            if rx.search(line):
                print(item['file'].split('\\')[-1], '::', line.strip()[:160])
                hits += 1
                if hits >= limit:
                    return
