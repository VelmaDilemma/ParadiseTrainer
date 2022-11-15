using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Memory;


namespace Paradise_Trainer
{
    public partial class Form1 : Form
    {
    //Constructor
        public Form1() 
        {
            InitializeComponent();
        }
    //Variables
        //Imports User 32 and the AsyncKeyState function for receiving input even if the program is not in focus
        [DllImport("User32.dll")]
        private static extern int GetAsyncKeyState(int i);
        //Class constructors
        Mem m = new Mem(); //Default constructor for memory class //Todo with Memory: Make a .ini as specified in documentation. Add a system to detect when module not found, and try a different pointer.
        Checkpoints ch = new Checkpoints();  //Default constructor for checkpoint class
        Rebinding rb = new Rebinding(); //Default constructor for rebinding class
        //Base Memory Addresses
        long playerBase; //Contains player position and angle
        long gravityBase; //Contains gravity and speed
        string AOBtext = "AOB Scan";
        //Button Array
        List<Button> buttonArray = new List<Button>(); //For certain functions to be made easy, all buttons within the program must be stored within an list for easy incrementation
        //Flight related variables
        float[] flyPosition = { 0, 0, 0 };
        bool[] clickedDownDirections = new bool[6]; //If a player is clicking on a button that controls flight, this will keep track of that in the order: +x,-x,+y,-y,+z,-z
        bool[] flyingDirections = new bool[6]; //Keeps track of the 6 directions of flight and which way a player wishes to go within them in the order: +x,-x,+y,-y,+z,-z

        long SingleAoBScanResult = 0;

    //Main form code
        private void Form1_Load(object sender, EventArgs e) //What happens when the form first loads
        {
            if (!backgroundWorker1.IsBusy) //If the worker is currently not doing anything initialize it so it will do things
                backgroundWorker1.RunWorkerAsync();
            try //Wrapped in a try in case the file does not currently exist
            {
                ch.ReadFromFile();//Reads in the saved checkpoint values
                rb.ReadFromFile();//Reads in the saved keybinding values
            }
            catch { }

            //Adds all buttons in the program to an array for easy access
            AddButtonsToList();//Adds all buttons in the program to an array for easy access
            for (int i = 0; i < 6; i++) //Sets up the array that keeps track of the directions in which the player wishes to go
                flyingDirections[i] = false;
        }
        //TODO clean up
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)//What happens when the form is open
        {
            //Executed once when the background worker is first called
            updateCheckpointLabels();
            m.WriteMemory((gravityBase + 0x150).ToString("X"), "Float", "1.2"); //Reset gravity

            while (true) //Continually executed as long as the background worker is active
            {
                int gameID = m.GetProcIdFromName("ParadiseKiller-Win64-Shipping");
                bool openGame = false; //Stores if the program was able to open the game
                if (gameID > 0) //If the ID is valid, then attempt to open the game and store the results of said opening in a boolean
                    openGame = m.OpenProcess(gameID);
                if (openGame) //If it can open the game's memory, then do below
                {
                    invokeTexts(); //Prints all labels for the form
                    Speedometer(); //Runs all code related to updating the speedometer
                    if (flightToggle.Checked) //Runs code related to flying
                    {
                        for (int i = 0; i < 6; i++) //Essential an OR gate, if the player has activated a fly button by either keybind or clicking it, set that direction to true
                            flyingDirections[i] = flyingDirections[i] || clickedDownDirections[i];
                        if (!rebind.Checked) //If not rebinding, then allow flight and send through the player's desired direction
                            flight(flyingDirections[0], flyingDirections[1], flyingDirections[2], flyingDirections[3], flyingDirections[4], flyingDirections[5], int.Parse(flySpeedTextBox.Text));
                        for (int i = 0; i < 6; i++) //Resets the flying directions for the next tick
                            flyingDirections[i] = false;
                        gravityComboBox.Invoke((MethodInvoker)delegate { //Deals with updating the gravity combobox when flying, namely disabling it and setting it to a particular value
                            gravityComboBox.Text = "0.0001  (Low Gravity)";
                            gravityComboBox.Enabled = false;
                        });
                    }
                    List<string> bindsPressed = rb.bindPressed(); //Grabs all keybinds that were pressed during the tick
                    if (bindsPressed.Count != 0 && !rebind.Checked) //If a keybind has been pressed and the player is not currently in rebind mode
                    {
                        for (int i = 0; i < bindsPressed.Count; i++) //Runs through all keybinds that were pressed
                            buttonActions(bindsPressed.ElementAt(i)); //Does the action associatede with the button pressed
                    }
                }
            }
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e) //What happens when the form is closed
        {
            m.WriteMemory((gravityBase + 0x150).ToString("X"), "Float", "1.2");
            ch.WriteToFile(); //Writes the current checkpoints to a file so they can be read in in future sessions
            rb.WriteToFile(); //Writes the current keybinds to a file so they can be read in in future sessions
        }

    //Useful Functions (used throughout code)
        private float[] CoordinatesArray() //Creates an array with the player's current coordinates
        {
            float[] cArray = new float[3];
            try
            {
                cArray[0] = m.ReadFloat((playerBase + 0x1D4).ToString("X")); //x
                cArray[1] = m.ReadFloat((playerBase + 0x1D0).ToString("X")); //y
                cArray[2] = m.ReadFloat((playerBase + 0x1D8).ToString("X")); //z
            }
            catch (Exception e) //Catches if the memory does not correspond to a float, for instance when the game is first opening.
            {
                System.Diagnostics.Debug.WriteLine("Issue reading memory: ParadiseKiller - Win64 - Shipping.exe + 0x03727AD8, 0x20, 0xC0, 0xA8, 0x170, 0x278, 0x130, 0x1D0");
                for (int i = 0; i < 3; i++)
                    cArray[i] = 0;
            }
            return cArray;
        }
        private void GoToLocation(float[] desiredPositionArray) //Takes the player to a specified position via a float array
        {
            m.WriteMemory((playerBase + 0x1D4).ToString("X"), "float", desiredPositionArray[0].ToString()); //Writes player's X to the given X
            m.WriteMemory((playerBase + 0x1D0).ToString("X"), "float", desiredPositionArray[1].ToString()); //Y
            m.WriteMemory((playerBase + 0x1D8).ToString("X"), "float", desiredPositionArray[2].ToString()); //Z
        }

    //Flight and Speedometer
        private void flight(bool px, bool mx, bool py, bool my, bool pz, bool mz, int speed)
        {
            float angle; //Stores the player's horizontal look angle in degrees relative to the positive y-axis
            try { angle = m.ReadFloat((playerBase + 0x1B4).ToString("X")); } //Attempts to get the angle from memory
            catch(Exception e) //Catches if the memory does not correspond to a float, for instance when the game is first opening.
            {
                System.Diagnostics.Debug.WriteLine("Problem reading memory: Angle");
                angle = 0;
            }
            float angleShift = 0; //Shifts the starting location of the angle to match with the players desired direction relative to forward, i.e. strafing to the right will shift it 90 degrees
            bool up = false; bool down = false; bool left = false; bool right = false; //Horizontal directions
            if (px && !mx)
                right = true;
            if (mx && !px)
                left = true;
            if (py && !my)
                up = true;
            if (my && !py)
                down = true;
            if (pz && !mz)
                flyPosition[2] += speed;
            if (mz && !pz)
                flyPosition[2] -= speed;
            //Calculate shift
            if (up) angleShift = 0;
            if (right) angleShift = 90f;
            if (down) angleShift = 180f;
            if (left) angleShift = 270f;
            if (up && right) angleShift = 45f;
            if (down && right) angleShift = 135f;
            if (down && left) angleShift = 225f;
            if (up && left) angleShift = 315f;
            if (right || left || up || down) //If the player is trying to fly somewhere
            {
                flyPosition[0] += speed * (float)Math.Sin((Math.PI / 180) * (angle + angleShift)); //Calculates the X component of the flight vector based on the angle and shift
                flyPosition[1] += speed * (float)Math.Cos((Math.PI / 180) * (angle + angleShift)); //Y component
            }
            m.FreezeValue((playerBase + 0x1D4).ToString("X"), "float", flyPosition[0].ToString()); //Freezes the X value to its new location the player has flied to in this tick
            m.FreezeValue((playerBase + 0x1D0).ToString("X"), "float", flyPosition[1].ToString()); //y
            m.FreezeValue((playerBase + 0x1D8).ToString("X"), "float", flyPosition[2].ToString()); //z
            }
        private void Speedometer()
        {
            float xVelocity; 
            float yVelocity;
            try
            {
                xVelocity = m.ReadFloat((gravityBase + 0xC8).ToString("X"));
                yVelocity = m.ReadFloat((gravityBase + 0xC4).ToString("X"));
            }
            catch(Exception e)//Catches if the memory does not correspond to a float, for instance when the game is first opening.
            {
                System.Diagnostics.Debug.WriteLine("Issue reading memory: Speed");
                xVelocity = 0;
                yVelocity = 0;
            }
            double speed = Math.Sqrt(Math.Pow((double)xVelocity, 2) + Math.Pow((double)yVelocity, 2)); //Pythagorean Thm.
            speedLabel.Invoke((MethodInvoker)delegate { speedLabel.Text = "Speed: " + Math.Round(speed); }); //Changes the text of the speedometer
        }

    //Various functions that update arrays or text values
        private void updateCheckpointLabels() //Run at startup to update the labels of checkpoints
        {
            textBox1.Invoke((MethodInvoker)delegate { textBox1.Text = ch.UpdatedLabel(0);});
            textBox2.Invoke((MethodInvoker)delegate { textBox2.Text = ch.UpdatedLabel(1);});
            textBox3.Invoke((MethodInvoker)delegate { textBox3.Text = ch.UpdatedLabel(2);});
            textBox4.Invoke((MethodInvoker)delegate { textBox4.Text = ch.UpdatedLabel(3);});
            textBox5.Invoke((MethodInvoker)delegate { textBox5.Text = ch.UpdatedLabel(4);});
            textBox6.Invoke((MethodInvoker)delegate { textBox6.Text = ch.UpdatedLabel(5);});
            textBox7.Invoke((MethodInvoker)delegate { textBox7.Text = ch.UpdatedLabel(6);});
            textBox8.Invoke((MethodInvoker)delegate { textBox8.Text = ch.UpdatedLabel(7);});
            textBox9.Invoke((MethodInvoker)delegate { textBox9.Text = ch.UpdatedLabel(8);});
        }
        private void invokeTexts() //Run every tick to update various text values across the software
        {
            float[] playerPosition = CoordinatesArray();
            //Writes current position
            labelX.Invoke((MethodInvoker)delegate { labelX.Text = "X: " + ((int)playerPosition[0]).ToString(); });
            labelY.Invoke((MethodInvoker)delegate { labelY.Text = "Y: " + ((int)playerPosition[1]).ToString(); });
            labelZ.Invoke((MethodInvoker)delegate { labelZ.Text = "Z: " + ((int)playerPosition[2]).ToString(); });
            //See if you can figure out a for loop here
            checkpointLabel0.Invoke((MethodInvoker)delegate { checkpointLabel0.Text = ch.LabelString(0); });
            checkpointLabel1.Invoke((MethodInvoker)delegate { checkpointLabel1.Text = ch.LabelString(1); });
            checkpointLabel2.Invoke((MethodInvoker)delegate { checkpointLabel2.Text = ch.LabelString(2); });
            checkpointLabel3.Invoke((MethodInvoker)delegate { checkpointLabel3.Text = ch.LabelString(3); });
            checkpointLabel4.Invoke((MethodInvoker)delegate { checkpointLabel4.Text = ch.LabelString(4); });
            checkpointLabel5.Invoke((MethodInvoker)delegate { checkpointLabel5.Text = ch.LabelString(5); });
            checkpointLabel6.Invoke((MethodInvoker)delegate { checkpointLabel6.Text = ch.LabelString(6); });
            checkpointLabel7.Invoke((MethodInvoker)delegate { checkpointLabel7.Text = ch.LabelString(7); });
            checkpointLabel8.Invoke((MethodInvoker)delegate { checkpointLabel8.Text = ch.LabelString(8); });
            aobScanButton.Invoke((MethodInvoker)delegate { aobScanButton.Text = AOBtext; });
            try { gravityLabel.Invoke((MethodInvoker)delegate { gravityLabel.Text = "Gravity: " + (m.ReadFloat((gravityBase + 0x150).ToString("X"))).ToString(); }); }
            catch (Exception e) { System.Diagnostics.Debug.WriteLine("Problem reading memory: ParadiseKiller - Win64 - Shipping.exe + 037450C0, 0x58, 0x278, 0x280, 0x150"); }
        }
        private void AddButtonsToList() //Run at startup, Adds all buttons in the program to an array for easy access
        {
            buttonArray.Add(setButton0);
            buttonArray.Add(setButton1);
            buttonArray.Add(setButton2);
            buttonArray.Add(setButton3);
            buttonArray.Add(setButton4);
            buttonArray.Add(setButton5);
            buttonArray.Add(setButton6);
            buttonArray.Add(setButton7);
            buttonArray.Add(setButton8);
            buttonArray.Add(goButton0);
            buttonArray.Add(goButton1);
            buttonArray.Add(goButton2);
            buttonArray.Add(goButton3);
            buttonArray.Add(goButton4);
            buttonArray.Add(goButton5);
            buttonArray.Add(goButton6);
            buttonArray.Add(goButton7);
            buttonArray.Add(goButton8);
            buttonArray.Add(fPlusXButton);
            buttonArray.Add(fMinusXButton);
            buttonArray.Add(fPlusYButton);
            buttonArray.Add(fMinusYButton);
            buttonArray.Add(fPlusZButton);
            buttonArray.Add(fMinusZButton);
            buttonArray.Add(xMinusQuickButton);
            buttonArray.Add(xPlusQuickButton);
            buttonArray.Add(yMinusQuickButton);
            buttonArray.Add(yPlusQuickButton);
            buttonArray.Add(zMinusQuickButton);
            buttonArray.Add(zPlusQuickButton);
            buttonArray.Add(teleportButton);
        }

    //Button Related Code
        private void buttonActions(string address) //The various actions a button can perform, called by their address
        {
            float[] cArray = CoordinatesArray();
            switch (address)
            { 
                case "0": //SetCheckpoint0
                case "00":
                    ch.StoreCheckpoints(0, CoordinatesArray());
                    break;
                case "01": //SetCheckpoint1
                case "1":
                    ch.StoreCheckpoints(1, CoordinatesArray());
                    break;
                case "02": //SetCheckpoint2
                case "2":
                    ch.StoreCheckpoints(2, CoordinatesArray());
                    break;
                case "03": //SetCheckpoint2
                case "3":
                    ch.StoreCheckpoints(3, CoordinatesArray());
                    break;
                case "04": //SetCheckpoint2
                case "4":
                    ch.StoreCheckpoints(4, CoordinatesArray());
                    break;
                case "05": //SetCheckpoint2
                case "5":
                    ch.StoreCheckpoints(5, CoordinatesArray());
                    break;
                case "06": //SetCheckpoint2
                case "6":
                    ch.StoreCheckpoints(6, CoordinatesArray());
                    break;
                case "07": //SetCheckpoint2
                case "7":
                    ch.StoreCheckpoints(7, CoordinatesArray());
                    break;
                case "08": //SetCheckpoint2
                case "8":
                    ch.StoreCheckpoints(8, CoordinatesArray());
                    break;
                case "10": //GoToCheckpoint0
                    GoToLocation(ch.ReadCheckpoints(0));
                    flyPosition = ch.ReadCheckpoints(0);
                    break;
                case "11": //GoToCheckpoint1
                    GoToLocation(ch.ReadCheckpoints(1));
                    flyPosition = ch.ReadCheckpoints(1);
                    break;
                case "12": //GoToCheckpoint2
                    GoToLocation(ch.ReadCheckpoints(2));
                    flyPosition = ch.ReadCheckpoints(2);
                    break;
                case "13": //GoToCheckpoint2
                    GoToLocation(ch.ReadCheckpoints(3));
                    flyPosition = ch.ReadCheckpoints(3);
                    break;
                case "14": //GoToCheckpoint2
                    GoToLocation(ch.ReadCheckpoints(4));
                    flyPosition = ch.ReadCheckpoints(4);
                    break;
                case "15": //GoToCheckpoint2
                    GoToLocation(ch.ReadCheckpoints(5));
                    flyPosition = ch.ReadCheckpoints(5);
                    break;
                case "16": //GoToCheckpoint2
                    GoToLocation(ch.ReadCheckpoints(6));
                    flyPosition = ch.ReadCheckpoints(6);
                    break;
                case "17": //GoToCheckpoint2
                    GoToLocation(ch.ReadCheckpoints(7));
                    flyPosition = ch.ReadCheckpoints(7);
                    break;
                case "18": //GoToCheckpoint2
                    GoToLocation(ch.ReadCheckpoints(8));
                    flyPosition = ch.ReadCheckpoints(8);
                    break;
                case "20": //+X
                    flyingDirections[0] = true;
                    break;
                case "21": //-X
                    flyingDirections[1] = true;
                    break;
                case "22": //+Y
                    flyingDirections[2] = true;
                    break;
                case "23": //-Y
                    flyingDirections[3] = true;
                    break;
                case "24": //+Z
                    flyingDirections[4] = true;
                    break;
                case "25": //-Z
                    flyingDirections[5] = true;
                    break;
                case "30": //Quick Teleport -X
                    cArray[0] -= 1000f;
                    GoToLocation(cArray);
                    break;
                case "31": //Quick Teleport +X
                    cArray[0] += 1000f;
                    GoToLocation(cArray);
                    break;
                case "32": //Quick Teleport -Y
                    cArray[1] -= 1000f;
                    GoToLocation(cArray);
                    break;
                case "33"://Quick Teleport +Y
                    cArray[1] += 1000f;
                    GoToLocation(cArray);
                    break;
                case "34": //Quick Teleport -Z
                    cArray[2] -= 1000f;
                    GoToLocation(cArray);
                    break;
                case "35": //Quick Teleport +Z
                    cArray[2] += 1000f;
                    GoToLocation(cArray);
                    break;
                case "40": //Teleport Button
                    float[] teleportArray = new float[3];
                    teleportArray[0] = float.Parse(teleportXTextbox.Text);
                    teleportArray[1] = float.Parse(teleportYTextbox.Text);
                    teleportArray[2] = float.Parse(teleportZTextbox.Text);
                    GoToLocation(teleportArray);
                    break;
            }
        }
        private void buttonPressed(Button button) //Called when a button is pressed
        {
            if (rebind.Checked) //If in rebind mode, wait for an input and then make that input the new keybinding for the button
                button.Text = rb.changeBind(button.Tag.ToString());
            else //If not in rebind mode, execute the action associated with the button, specified by it's address in it's data tag
            {
                string data = button.Tag.ToString();
                string hexAddress = data.Substring(0, 2);
                buttonActions(hexAddress);
            }
        }



    //Deals with Gravity ComboBox
        private void gravityComboBox_TextChanged(object sender, EventArgs e)
        {
            float gravity;
            try //Attempts to decipher what is in the textbox
            {
                int findParenth = (gravityComboBox.Text).IndexOf("("); //Looks for a parenthesis in the text
                if (findParenth == -1) //If there is no parenthesis try to parse for a float
                    gravity = float.Parse(gravityComboBox.Text);
                else //If there is a parenthesis parse for a float in the text that comes before the parenthesis
                    gravity = float.Parse((gravityComboBox.Text).Substring(0, findParenth));
            }
            catch { gravity = 1.2f; } //If it is unable to parse a float, set gravity to it's default
            m.WriteMemory((gravityBase + 0x150).ToString("X"), "Float", gravity.ToString());
        }
    //If rebind mode is toggled on/off
        private void rebind_CheckedChanged(object sender, EventArgs e) //change all buttons back to their text or to their keybinds
        {
            if (rebind.Checked) //If in rebind mode, set the buttons text to be their keybind
            {
                for (int i=0; i<buttonArray.Count; i++)
                    buttonArray[i].Text = rb.displayBinds(buttonArray[i].Tag.ToString());
            }
            else
            {
                for (int i = 0; i < buttonArray.Count; i++) //If not in rebind mode, set all buttons back to their original text as specified in their data tags
                {
                    string data = buttonArray[i].Tag.ToString();
                    buttonArray[i].Text = data.Substring(3);
                }
            }
        }
    //If flight mode is toggled on/off
        private void flightToggle_CheckedChanged(object sender, EventArgs e)
        {
            flyPosition = CoordinatesArray(); //Initializes the flight position
            //TODO: save gravity before a player toggles flight, return it to their previous
            gravityComboBox.Invoke((MethodInvoker)delegate { //Attempts to reset the combobox, only succeeds if flight is false
                gravityComboBox.Text = "1.2 (Default)";
                gravityComboBox.Enabled = true;
            });
            m.UnfreezeValue((playerBase + 0x1D4).ToString("X")); //Attempts to unfreeze memory, only succeeds if flight is false
            m.UnfreezeValue((playerBase + 0x1D0).ToString("X"));
            m.UnfreezeValue((playerBase + 0x1D8).ToString("X"));
        }
    //Receives a button click from the program and calls the corresponding function
        private void setButton0_Click(object sender, EventArgs e) => buttonPressed(setButton0);
        private void setButton1_Click(object sender, EventArgs e) => buttonPressed(setButton1);
        private void setButton2_Click(object sender, EventArgs e) => buttonPressed(setButton2);
        private void setButton3_Click(object sender, EventArgs e) => buttonPressed(setButton3);
        private void setButton4_Click(object sender, EventArgs e) => buttonPressed(setButton4);
        private void setButton5_Click(object sender, EventArgs e) => buttonPressed(setButton5);
        private void setButton6_Click(object sender, EventArgs e) => buttonPressed(setButton6);
        private void setButton7_Click(object sender, EventArgs e) => buttonPressed(setButton7);
        private void setButton8_Click(object sender, EventArgs e) => buttonPressed(setButton8);
        private void goButton0_Click(object sender, EventArgs e) => buttonPressed(goButton0);
        private void goButton1_Click(object sender, EventArgs e) => buttonPressed(goButton1);
        private void goButton2_Click(object sender, EventArgs e) => buttonPressed(goButton2);
        private void goButton3_Click(object sender, EventArgs e) => buttonPressed(goButton3);
        private void goButton4_Click(object sender, EventArgs e) => buttonPressed(goButton4);
        private void goButton5_Click(object sender, EventArgs e) => buttonPressed(goButton5);
        private void goButton6_Click(object sender, EventArgs e) => buttonPressed(goButton6);
        private void goButton7_Click(object sender, EventArgs e) => buttonPressed(goButton7);
        private void goButton8_Click(object sender, EventArgs e) => buttonPressed(goButton8);
        private void fPlusXButton_Click(object sender, EventArgs e) => buttonPressed(fPlusXButton);
        private void fMinusXButton_Click(object sender, EventArgs e) => buttonPressed(fMinusXButton);
        private void fPlusYButton_Click(object sender, EventArgs e) => buttonPressed(fPlusYButton);
        private void fMinusYButton_Click(object sender, EventArgs e) => buttonPressed(fMinusYButton);
        private void fPlusZButton_Click(object sender, EventArgs e) => buttonPressed(fPlusZButton);
        private void fMinusZButton_Click(object sender, EventArgs e) => buttonPressed(fMinusZButton);
        private void xMinusQuickButton_Click(object sender, EventArgs e) => buttonPressed(xMinusQuickButton);
        private void xPlusQuickButton_Click(object sender, EventArgs e) => buttonPressed(xPlusQuickButton);
        private void yMinusQuickButton_Click(object sender, EventArgs e) => buttonPressed(yMinusQuickButton);
        private void yPlusQuickButton_Click(object sender, EventArgs e) => buttonPressed(yPlusQuickButton);
        private void zMinusQuickButton_Click(object sender, EventArgs e) => buttonPressed(zMinusQuickButton);
        private void zPlusQuickButton_Click(object sender, EventArgs e) => buttonPressed(zPlusQuickButton);
        private void teleportButton_Click(object sender, EventArgs e) => buttonPressed(teleportButton);
    //Deals with holding down any of the flight buttons
        private void fPlusXButton_MouseDown(object sender, MouseEventArgs e) => clickedDownDirections[0] = true;
        private void fPlusXButton_MouseUp(object sender, MouseEventArgs e) => clickedDownDirections[0] = false;
        private void fMinusXButton_MouseDown(object sender, MouseEventArgs e) => clickedDownDirections[1] = true;
        private void fMinusXButton_MouseUp(object sender, MouseEventArgs e) => clickedDownDirections[1] = false;
        private void fPlusYButton_MouseDown(object sender, MouseEventArgs e) => clickedDownDirections[2] = true;
        private void fPlusYButton_MouseUp(object sender, MouseEventArgs e) => clickedDownDirections[2] = false;
        private void fMinusYButton_MouseDown(object sender, MouseEventArgs e) => clickedDownDirections[3] = true;
        private void fMinusYButton_MouseUp(object sender, MouseEventArgs e) => clickedDownDirections[3] = false;
        private void fPlusZButton_MouseDown(object sender, MouseEventArgs e) => clickedDownDirections[4] = true;
        private void fPlusZButton_MouseUp(object sender, MouseEventArgs e) => clickedDownDirections[4] = false;
        private void fMinusZButton_MouseDown(object sender, MouseEventArgs e) => clickedDownDirections[5] = true;
        private void fMinusZButton_MouseUp(object sender, MouseEventArgs e) => clickedDownDirections[5] = false;
    //Deals with user's checkpoints labels
        private void textBox1_TextChanged(object sender, EventArgs e) => ch.LabelChanged(textBox1.Text, 0);
        private void textBox2_TextChanged(object sender, EventArgs e) => ch.LabelChanged(textBox2.Text, 1);
        private void textBox3_TextChanged(object sender, EventArgs e) => ch.LabelChanged(textBox3.Text, 2);
        private void textBox4_TextChanged(object sender, EventArgs e) => ch.LabelChanged(textBox4.Text, 3);
        private void textBox5_TextChanged(object sender, EventArgs e) => ch.LabelChanged(textBox5.Text, 4);
        private void textBox6_TextChanged(object sender, EventArgs e) => ch.LabelChanged(textBox6.Text, 5);
        private void textBox7_TextChanged(object sender, EventArgs e) => ch.LabelChanged(textBox7.Text, 6);
        private void textBox8_TextChanged(object sender, EventArgs e) => ch.LabelChanged(textBox8.Text, 7);
        private void textBox9_TextChanged(object sender, EventArgs e) => ch.LabelChanged(textBox9.Text, 8);
    //Reset Checkpoint Buttons
        private void resetCheckpoint(TextBox label, int index)
        {
            label.Invoke((MethodInvoker)delegate {label.Text = "";});
            float[] zeroArray = new float[3];
            for (int i = 0; i < 3; i++)
                zeroArray[i] = 0;
            ch.StoreCheckpoints(index,zeroArray);
        }
        private void resetButton0_Click(object sender, EventArgs e) => resetCheckpoint(textBox1, 0);
        private void resetButton1_Click(object sender, EventArgs e) => resetCheckpoint(textBox2, 1);
        private void resetButton2_Click(object sender, EventArgs e) => resetCheckpoint(textBox3, 2);
        private void resetButton3_Click(object sender, EventArgs e) => resetCheckpoint(textBox4, 3);
        private void resetButton4_Click(object sender, EventArgs e) => resetCheckpoint(textBox5, 4);
        private void resetButton5_Click(object sender, EventArgs e) => resetCheckpoint(textBox6, 5);
        private void resetButton6_Click(object sender, EventArgs e) => resetCheckpoint(textBox7, 6);
        private void resetButton7_Click(object sender, EventArgs e) => resetCheckpoint(textBox8, 7);
        private void resetButton8_Click(object sender, EventArgs e) => resetCheckpoint(textBox9, 8);

        private void aobScanButton_Click(object sender, EventArgs e)
        {
            doAScan();
        }
        public async void doAScan()
        {
            AOBtext = "Scanning...";
            IEnumerable<long> aobScanPlayer = await m.AoBScan("?? ?? ?? ?? ?? ?? ?? ?? 48 00 04 00 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 37 21 03 00 00 00 00 00 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 02 00 0C 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 FF FF FF FF FF FF FF FF 07 06 42 05 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", true, false);
            IEnumerable<long> aobScanGravity = await m.AoBScan("?? ?? ?? ?? ?? ?? ?? ?? 48 00 04 00 ?? ?? ?? ?? 80 3E ?? ?? ?? ?? ?? ?? 30 21 03 00 00 00 00 00 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 00 00 1E 01 00 00 00 00 ?? ?? ?? ?? ?? ?? ?? ?? 01 00 00 00 04 00 00 00 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 FF FF FF FF FF FF FF FF 01 86 D3 05 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", true, false);
            playerBase = aobScanPlayer.FirstOrDefault();
            gravityBase = aobScanGravity.FirstOrDefault();
            AOBtext = "AOB Scan";
        }
    }
}
    