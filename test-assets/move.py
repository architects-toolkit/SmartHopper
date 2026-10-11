import ctypes
import sys
import time
from ctypes import wintypes as w

u = ctypes.windll.user32
try:
    u.SetProcessDPIAware()
except Exception:
    pass


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [
        ("dx", w.LONG),
        ("dy", w.LONG),
        ("mouseData", w.DWORD),
        ("dwFlags", w.DWORD),
        ("time", w.DWORD),
        ("dwExtraInfo", ctypes.c_void_p),
    ]


class INPUT(ctypes.Structure):
    _fields_ = [("type", w.DWORD), ("mi", MOUSEINPUT)]


x, y = int(sys.argv[1]), int(sys.argv[2])
vx = u.GetSystemMetrics(76)
vy = u.GetSystemMetrics(77)
vw = u.GetSystemMetrics(78)
vh = u.GetSystemMetrics(79)
nx = int((x - vx) * 65535 / max(vw - 1, 1))
ny = int((y - vy) * 65535 / max(vh - 1, 1))
ABS = 0x8000 | 0x40000000
seq = [
    INPUT(0, MOUSEINPUT(nx, ny, 0, ABS | 0x0001, 0, None)),
    INPUT(0, MOUSEINPUT(nx + 40, ny + 20, 0, ABS | 0x0001, 0, None)),
    INPUT(0, MOUSEINPUT(nx, ny, 0, ABS | 0x0001, 0, None)),
]
n = u.SendInput(len(seq), (INPUT * len(seq))(*seq), ctypes.sizeof(INPUT))
print("moved", n, "-> screen", (x, y))
