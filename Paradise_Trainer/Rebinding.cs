using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Paradise_Trainer
{
    class Rebinding //Class with functions related to rebinding keys for the trainer
    {
        [DllImport("User32.dll")]
        private static extern int GetAsyncKeyState(int i); //Allows the program to read keyboard inputs
        int[] bindsArray = new int[256]; //Stores all keybinds for the program
        KeysConverter kc = new KeysConverter(); //Allows the program to convert from the integer value of a key press to a string describing said keypress
        //All buttons are given an address specified as hexadecimal in their data tag, this corresponds to their index in the binds array
        //Tags beginning with 0 are for set buttons, 1 for Goto buttons,
        public Rebinding() //constructor
        {
            for (int i=0; i<256; i++) //Sets all bind values to undefined
            {
                bindsArray[i] = 7;
            }
        }
        public string displayBinds(string data) //Returns a string for a button to display its current keybinding
        {
            string hexAddress = data.Substring(0, 2);
            int intAddress = System.Convert.ToInt32(hexAddress, 16); //converts the hexidecimal adress to an integer to use as an index
            string bindText = kc.ConvertToString(bindsArray[intAddress]); //Finds the key bind associated with the button
            return bindText;
        }
        public string changeBind(string data) //Changes a keybinding for a button and returns a string of the new keybind
        {
            string hexAddress = data.Substring(0, 2);
            int intAddress = System.Convert.ToInt32(hexAddress, 16); //converts the hexidecimal adress to an integer to use as an index
            int newBind;
            List<int> keysPressed = scanForKeypress();
            while (keysPressed.Count==0) //Waits for the user to press a button
            {
                keysPressed = scanForKeypress();
            }
            newBind = keysPressed.ElementAt(0);
            bindsArray[intAddress] = newBind;
            KeysConverter kc = new KeysConverter();
            string bindString = kc.ConvertToString(newBind);
            return bindString;
        }
        public List<string> bindPressed()
        {
            List<string> pressedAddresses = new List<string>(); //A list that stores all pushed binds' addresses
            List<int> keysPressed = scanForKeypress();
            for (int i = 0; i < 256; i++) //Checks to see if any key is currently being pressed that is bound to an action
            {
                for (int j = 0; j < keysPressed.Count; j++)
                {
                    if (keysPressed.ElementAt(j) == bindsArray[i]) //If a key being pressed matches a bind, add that bind's address to the list
                        pressedAddresses.Add(i.ToString("X")); 
                }
            }
            return pressedAddresses;
        }
        public void WriteToFile() //Writes the current saved checkpoints to a file to be read later
        {
            string[] stringBindings = new string[256];
            for (int i = 0; i < 256; i++)
                stringBindings[i] = bindsArray[i].ToString();
            File.WriteAllLines("Binds.txt", stringBindings);
        }
        public void ReadFromFile() //Writes the current saved checkpoints to a file to be read later
        {
            string[] stringBindings = File.ReadAllLines("Binds.txt");
            for (int i = 0; i < 256; i++)
                bindsArray[i] = int.Parse(stringBindings[i]);
        }
        private List<int> scanForKeypress() //Looks for a keypress from the user, Either returns the virtual keycode of what they pressed or 256 if nothing is pressed 
        {
            List<int> keysPressed = new List<int>();
            for (int i = 0; i < 256; i++) //Checks all hexadecimal key codes
            {
                int aKS = GetAsyncKeyState(i);
                if (aKS != 0) //If a key is being pressed
                {
                    keysPressed.Add(i);
                }
            }
            Thread.Sleep(10); //Scan for keypress will only be done once every 10 ms to prevent excess CPU usage
            return keysPressed; //If nothing is being pressed, an invalid address is returned to let the program know to keep waiting
        }
 
    }
}
