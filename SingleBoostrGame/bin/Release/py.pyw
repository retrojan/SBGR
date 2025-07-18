import sys
import json
import os
from PyQt5.QtWidgets import QApplication, QWidget, QVBoxLayout, QHBoxLayout, QPushButton, QLineEdit, QCheckBox, QLabel, QFormLayout
from PyQt5.QtGui import QIcon, QFont
from PyQt5.QtCore import Qt, QPropertyAnimation, QPoint

class Config:
    def __init__(self):
        self.app_ids = [730]
        self.minimize_to_tray = True
        self.autorun = True
        self.check_for_updates = True

class ConfigEditor(QWidget):
    def __init__(self):
        super().__init__()

        self.config = Config()
        self.config_file = "config.json"  # Путь к конфигурационному файлу
        self.init_ui()

    def init_ui(self):
        self.setWindowTitle('Config Editor 7.0')
        self.resize(400, 300)  # Устанавливаем размер окна (ширина, высота)
        self.setStyleSheet("background-color: #1E1E1E; color: white; font: 10pt 'Segoe UI';")

        # Отключаем стандартный титульный бар и создаем свой
        self.setWindowFlag(Qt.FramelessWindowHint)

        # Создаем кастомный титульный бар
        self.title_bar = QWidget(self)
        self.title_bar.setStyleSheet("background-color: #333;")
        self.title_bar.setGeometry(0, 0, self.width(), 40)  # Размер титульного бара
        self.title_bar.mousePressEvent = self.mouse_press_event
        self.title_bar.mouseMoveEvent = self.mouse_move_event

        # Добавляем название на титульный бар
        self.title_label = QLabel("Config Editor 7.0", self.title_bar)
        self.title_label.setStyleSheet("color: white; font-size: pt;")
        self.title_label.setGeometry(10, 10, 250, 20)

        # Добавляем кнопки на титульный бар
        self.close_button = QPushButton('×', self.title_bar)
        self.close_button.setStyleSheet("background-color: transparent; color: white; border: none; font-size: 14pt;")
        self.close_button.setGeometry(self.width() - 40, 5, 30, 30)
        self.close_button.clicked.connect(self.close)

        self.minimize_button = QPushButton('-', self.title_bar)
        self.minimize_button.setStyleSheet("background-color: transparent; color: white; border: none; font-size: 14pt;")
        self.minimize_button.setGeometry(self.width() - 70, 5, 30, 30)
        self.minimize_button.clicked.connect(self.minimize)

        # Добавляем анимацию для кнопок
        self.close_button.setStyleSheet("background-color: transparent; color: white; border: none; font-size: 14pt;")
        self.minimize_button.setStyleSheet("background-color: transparent; color: white; border: none; font-size: 14pt;")
        
        # Анимация для кнопки закрытия (красный цвет при наведении)
        self.close_button.setStyleSheet("""
            QPushButton {
                background-color: transparent;
                color: white;
                border: none;
                font-size: 14pt;
            }
            QPushButton:hover {
                background-color: red;
            }
        """)
                # Анимация для кнопки закрытия (красный цвет при наведении)
        self.minimize_button.setStyleSheet("""
            QPushButton {
                background-color: transparent;
                color: white;
                border: none;
                font-size: 14pt;
            }
            QPushButton:hover {
                background-color: #565656;
            }
        """)

        # Создаем контейнер для основного содержимого
        self.main_content = QWidget(self)
        self.main_content.setStyleSheet("background-color: #1E1E1E; color: white; font: 10pt 'Segoe UI';")
        self.main_content.setGeometry(0, 40, self.width(), self.height() - 40)  # Оставляем пространство под титульный бар

        # Создаем элементы интерфейса для основного содержимого
        self.app_id_input = QLineEdit(self.main_content)
        self.app_id_input.setText(str(self.config.app_ids[0]))
        self.app_id_input.setStyleSheet("background-color: #2E2E2E; color: white; border: 1px solid #555;")

        self.minimize_checkbox = QCheckBox("Minimize to Tray", self.main_content)
        self.minimize_checkbox.setChecked(self.config.minimize_to_tray)
        self.minimize_checkbox.setStyleSheet("color: white;")

        self.autorun_checkbox = QCheckBox("Autorun", self.main_content)
        self.autorun_checkbox.setChecked(self.config.autorun)
        self.autorun_checkbox.setStyleSheet("color: white;")

        self.update_checkbox = QCheckBox("Check for Updates", self.main_content)
        self.update_checkbox.setChecked(self.config.check_for_updates)
        self.update_checkbox.setStyleSheet("color: white;")

        self.save_button = QPushButton('Save', self.main_content)
        self.save_button.clicked.connect(self.save_config)
        self.save_button.setStyleSheet("background-color: #333333; color: white; padding: 8px; border-radius: 4px;")

        self.start_button = QPushButton('Start', self.main_content)
        self.start_button.clicked.connect(self.start_action)

        self.start_button.setStyleSheet("""
            QPushButton {
            background-color: #333333;
            color: white;
            padding: 8px; 
            border-radius: 4px;
            
            }
            QPushButton:hover {
                background-color: #565656;
            }
        """)
        self.save_button.setStyleSheet("""
            QPushButton {
            background-color: #333333;
            color: white;
            padding: 8px; 
            border-radius: 4px;
            
            }
            QPushButton:hover {
                background-color: #565656;
            }
        """)

        # Layout
        layout = QVBoxLayout(self.main_content)

        form_layout = QFormLayout()
        form_layout.addRow(QLabel("App ID"), self.app_id_input)
        form_layout.addRow(self.minimize_checkbox)
        form_layout.addRow(self.autorun_checkbox)
        form_layout.addRow(self.update_checkbox)

        layout.addLayout(form_layout)

        button_layout = QHBoxLayout()
        button_layout.addWidget(self.save_button)
        button_layout.addWidget(self.start_button)
        layout.addLayout(button_layout)

        # Загружаем конфигурацию при запуске (после инициализации UI)
        self.load_config()

    def mouse_press_event(self, event):
        if event.button() == Qt.LeftButton:
            self.offset = event.pos()
            event.accept()

    def mouse_move_event(self, event):
        if event.buttons() == Qt.LeftButton:
            self.move(self.pos() + event.pos() - self.offset)
            event.accept()

    def save_config(self):
        # Сохраняем конфигурацию в файл
        try:
            app_id = int(self.app_id_input.text())
        except ValueError:
            print("Invalid AppId")
            return

        self.config.app_ids = [app_id]
        self.config.minimize_to_tray = self.minimize_checkbox.isChecked()
        self.config.autorun = self.autorun_checkbox.isChecked()
        self.config.check_for_updates = self.update_checkbox.isChecked()

        config_data = {
            "AppIds": self.config.app_ids,
            "MinimizeToTray": self.config.minimize_to_tray,
            "Autorun": self.config.autorun,
            "CheckForUpdates": self.config.check_for_updates
        }

        # Сохраняем конфигурацию в файл с фиксированным именем
        with open(self.config_file, 'w') as f:
            json.dump(config_data, f, indent=4)
        print("Configuration saved!")

    def load_config(self):
        # Загружаем конфигурацию из файла
        try:
            with open(self.config_file, 'r') as f:
                config_data = json.load(f)

            self.config.app_ids = config_data.get('AppIds', [730])
            self.config.minimize_to_tray = config_data.get('MinimizeToTray', True)
            self.config.autorun = config_data.get('Autorun', True)
            self.config.check_for_updates = config_data.get('CheckForUpdates', True)

            # Обновляем UI
            self.app_id_input.setText(str(self.config.app_ids[0]))
            self.minimize_checkbox.setChecked(self.config.minimize_to_tray)
            self.autorun_checkbox.setChecked(self.config.autorun)
            self.update_checkbox.setChecked(self.config.check_for_updates)

            print("Configuration loaded!")
        except FileNotFoundError:
            print("Config file not found. Using default settings.")
            self.save_config()  # Сохраняем конфигурацию по умолчанию, если файл не найден

    def start_action(self):
        try:
            os.startfile('SBGR.exe')
        except FileNotFoundError:
            print("SBGR.exe not found!")

    def minimize(self):
        self.showMinimized()

if __name__ == '__main__':
    app = QApplication(sys.argv)
    editor = ConfigEditor()
    editor.show()
    sys.exit(app.exec_())
