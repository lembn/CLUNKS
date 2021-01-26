from gui.CustomWidgets import RootWindow
from gui.windows.MainWindow import MainWindow

if __name__ == "__main__":
    root = RootWindow(theme='equilux')
    mainWindow = MainWindow(root, 600, 400)
    root.mainloop()