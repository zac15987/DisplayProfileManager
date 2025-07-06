Please help me write a small C# program to run on Windows.

The main function is to set the system’s screen resolution and DPI scaling. When the program launches, it should first read the current system resolution and DPI scaling, and save them as a default profile in a JSON file. Users should be able to create additional profiles. Whenever a profile is switched, the program should immediately apply the corresponding resolution and DPI scaling.

Additionally, the settings should include the option to configure whether the program starts automatically with Windows, and which profile to execute upon startup.

Once launched, the program should run in the background in the System Tray, without an independent window. When users want to configure or switch profiles, they can right-click the program’s icon in the System Tray. A context menu should appear, allowing them to select which profile to use.

If users want to edit profile details, the program should pop up a window for configuration, where they can set the desired resolution and DPI for that profile. Upon saving, the profile data should be saved as a JSON file and stored in the application’s AppData folder.

The UI should follow the modern Windows 11 design style.
