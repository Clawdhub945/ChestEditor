import io, re

BS = chr(92)
NL = chr(10)

def strip_line(line):
    """去掉字符串/字符字面量/注释后返回用于深度计数的文本。
    状态机处理内插字符串 $\"...\" 的嵌套洞与洞内字符串。"""
    out = []
    stack = []  # ['s']=string, ['c']=char, ['i']=interp-string, ['h']=interp-hole
    i, n = 0, len(line)
    while i < n:
        c = line[i]
        top = stack[-1] if stack else None
        if top is None:
            if c == '/' and i + 1 < n and line[i + 1] == '/':
                break
            if c == '"':
                if out and out[-1] == '$':
                    out[-1] = ' '
                    stack.append(['i'])
                    i += 1
                    continue
                stack.append(['s'])
                i += 1
                continue
            if c == "'":
                stack.append(['c'])
                i += 1
                continue
            out.append(c)
            i += 1
            continue
        kind = top[0]
        if kind == 's':
            if c == BS:
                i += 2
                continue
            if c == '"':
                stack.pop()
            i += 1
            continue
        if kind == 'c':
            if c == BS:
                i += 2
                continue
            if c == "'":
                stack.pop()
            i += 1
            continue
        if kind == 'i':
            if c == BS:
                i += 2
                continue
            if c == '"':
                if i + 1 < n and line[i + 1] == '"':
                    i += 2
                    continue
                stack.pop()
                i += 1
                continue
            if c == '{':
                if i + 1 < n and line[i + 1] == '{':
                    i += 2
                    continue
                stack.append(['h', 0])
                i += 1
                continue
            i += 1
            continue
        # interp hole
        hd = top[1]
        if c == '{':
            hd += 1
            top[1] = hd
            i += 1
            continue
        if c == '}':
            if hd == 0:
                stack.pop()
            else:
                hd -= 1
                top[1] = hd
            i += 1
            continue
        if c == '"':
            if out and out[-1] == '$':
                out[-1] = ' '
                stack.append(['i'])
                i += 1
                continue
            stack.append(['s'])
            i += 1
            continue
        if c == "'":
            stack.append(['c'])
            i += 1
            continue
        out.append(c)
        i += 1
    return ''.join(out)

def split_members(path, class_decl):
    """static class 的成员拆成块列表 [(text, start_line, end_line)]，每块带前置注释"""
    lines = io.open(path, encoding='utf-8').read().split(NL)
    start = None
    for idx, l in enumerate(lines):
        if class_decl in l:
            start = idx
            break
    if start is None:
        raise SystemExit('class not found: ' + class_decl)
    depth = 0
    i = start
    while i < len(lines):
        depth += strip_line(lines[i]).count('{') - strip_line(lines[i]).count('}')
        i += 1
        if depth >= 1:
            break
    blocks = []
    block_start = i
    depth = 0
    j = i
    while j < len(lines):
        s = strip_line(lines[j]).strip()
        if s and not s.startswith('//') and not s.startswith('///'):
            depth += s.count('{') - s.count('}')
            if depth <= 0 and (s.endswith(';') or s.endswith('}')):
                blocks.append((block_start, j))
                block_start = j + 1
                depth = 0
        j += 1
    return [(NL.join(lines[a:b + 1]), a + 1, b + 1) for (a, b) in blocks]

def pick(members, pattern):
    return [(t, a, b) for (t, a, b) in members if re.search(pattern, t)]

def first_code_line(text):
    for l in text.split(NL):
        s = l.strip()
        if s and not s.startswith('//') and not s.startswith('///'):
            return s
    return ''

def pick_sig(members, pattern):
    """只匹配块的第一行代码（成员签名/字段声明）"""
    return [(t, a, b) for (t, a, b) in members if re.search(pattern, first_code_line(t))]

def dump(members, title='members'):
    print('--- ' + title + ' ---')
    for (text, a, b) in members:
        first = next((l.strip() for l in text.split(NL) if l.strip() and not l.strip().startswith('//')), '?')
        print(f'{a}-{b}: {first[:90]}')

def write_file(path, body):
    io.open(path, 'w', encoding='utf-8', newline=NL).write(body)
    print('written', path)
