For use with the Sapphire Server emulator:

https://github.com/SapphireServer/Sapphire

# Overview
Monster Gambit Editor provides a user-friendly interface for creating and editing monster behavior patterns. 
The application features a dual-panel layout with a visual editor on the left and a JSON editor on the right, allowing you to work in whichever format you prefer while maintaining perfect synchronization between both views.

# Features
Monster Management
-	Monster Selection: Choose monsters from a dropdown menu
-	Automatic JSON Highlighting: Selected monster's JSON data is highlighted in the text editor
-	Monster Properties: Edit base ID, name ID, attack range, and ranged attack settings

# Gambit System
-	Visual Gambit Editor: Create and manage monster behavior rules through an intuitive interface
-	Gambit Properties:
-	Timing: Control when each action occurs
-	Conditions: Select from various targeting conditions (self, player, ally, etc.)
-	Actions: Choose from a comprehensive list of possible monster actions
-	Enable/Disable: Toggle gambits without deleting them
-	HP Thresholds: Configure HP percentage-based conditions (Work in Progress)
	 
# Advanced Features
-	Bi-directional Synchronization: Changes in either panel are instantly reflected in the other
-	Loop Control: Set finite or infinite action loops
-	Batch Operations: Add new gambits or delete disabled ones with a single click
-	Visual Indicators: Color-coded enabled/disabled status

# Editor Capabilities
-	JSON Syntax Highlighting: Makes reading and editing JSON easier
-	Search Functionality: Find specific text across the document
-	File Operations: Open, save, and export your work

# Getting Started
1.	Open a File: Click the File dropdown and select Open to load a JSON file
2.	Select a Monster: Choose a monster from the dropdown to view and edit its gambits
3.	Edit Properties: Modify monster properties like attack range and IDs
4.	Manage Gambits: Add new gambits, modify existing ones, or disable unwanted behaviors
5.	Save Your Work: Click Save to store your changes

# Technical Details
-	Built with .NET 8
-	Uses FastColoredTextBox for enhanced text editing capabilities
-	Implements real-time synchronization between visual editor and JSON data

# Requirements
-	Windows 10 or later
-	.NET 8 Runtime