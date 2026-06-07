import re

patterns = [
    "Unable to find item to add dependency",
    "Couldn't find .dll:",
    "Could not find the event index for:",
    "Unable to find particle system with name",
    "Unable to recover legacy particle system",
    "FMOD error!",
    "Emitter hierarchy does not match",
    "Error: Non-Zero Device Reference Count!"
]

errors = set()
with open('rgl_log_35552.txt', 'r', encoding='utf-8') as f:
    for line in f:
        # Remove timestamp [HH:MM:SS.ms]
        line = re.sub(r'^\[\d{2}:\d{2}:\d{2}\.\d{3}\]\s*', '', line).strip()
        if not line:
            continue

        for p in patterns:
            if line.startswith(p) or p in line:
                errors.add(line)

with open('error_dict.txt', 'w', encoding='utf-8') as f:
    for e in sorted(errors):
        f.write(e + '\n')
print(f"Extracted {len(errors)} unique errors.")
