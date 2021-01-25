from gui.CustomWidgets import RootWindow
from gui.windows.MainWindow import MainWindow

if __name__ == "__main__":
    root = RootWindow(theme='adapta')
    mainWindow = MainWindow(root, 240, 270)
    root.mainloop()