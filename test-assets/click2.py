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


def send(flags):
    inp = INPUT(0, MOUSEINPUT(nx, ny, 0, ABS | flags, 0, None))
    return u.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))


if len(sys.argv) > 3:
    u.SetForegroundWindow(int(sys.argv[3]))
    time.sleep(0.3)
send(0x0001)          # MOVE
time.sleep(0.15)
send(0x0002)          # LEFTDOWN
time.sleep(0.12)
send(0x0004)          # LEFTUP
print("clicked", (x, y))
