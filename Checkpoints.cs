using System.IO;

namespace Paradise_Trainer
{
    class Checkpoints //Basic class to store all checkpoint locations and save them to a file
    {
        //Todo: change array to array list
        float[] checkpointPosition = new float[36]; //Creates an array which can store 12 sets of coordinates for various checkpoints
        string[] checkpointLabels = new string[12];
        public Checkpoints() { }
        public void StoreCheckpoints (int checkpointNumber, float[] cArray) //Stores the current position as a checkpoint. Takes in which checkpoint to store it in i.e. if button 1 is pressed it will store x,y,z in the first three spaces, etc.
        {
            int index = checkpointNumber * 3;
            checkpointPosition[index] = cArray[0]; //Changes the checkpoint position to match where the player currently is X
            checkpointPosition[index+1] = cArray[1]; //Y
            checkpointPosition[index+2] = cArray[2]; //Z
        }
        public float[] ReadCheckpoints(int checkpointNumber)
        {
            float[] chArray = new float[3];
            int index = checkpointNumber * 3;
            chArray[0] = checkpointPosition[index]; //X
            chArray[1] = checkpointPosition[index+1]; //Y
            chArray[2] = checkpointPosition[index+2]; //Z
            return chArray;
        }
        public string LabelString(int checkpointNumber) //Generates the label next to the checkpoint that contains the coordinates of said checkpoint
        {
            float[] chCoords = ReadCheckpoints(checkpointNumber); //Finds the coordinates of the checkpoint in question
            string label = "Coords: (" + (int)chCoords[0] + ", " + (int)chCoords[1] + ", " + (int)chCoords[2] + ")"; //Floats are truncated here for briefness
            return label;
        }
        public void LabelChanged(string labelText, int index) => checkpointLabels[index] = labelText;
        public string UpdatedLabel(int index) { return checkpointLabels[index]; }
        public void WriteToFile() //Writes the current saved checkpoints to a file to be read later
        {
            string[] stringCheckpoints = new string[36];
            for (int i = 0; i< 36; i++)
                stringCheckpoints[i] = checkpointPosition[i].ToString();
            File.WriteAllLines("Checkpoints.txt", stringCheckpoints);
            File.WriteAllLines("CheckpointLabels.txt", checkpointLabels);
        }
        public void ReadFromFile() //Writes the current saved checkpoints to a file to be read later
        {
            string[] stringCheckpoints = File.ReadAllLines("Checkpoints.txt");
            for (int i = 0; i < 36; i++)
                checkpointPosition[i] = float.Parse(stringCheckpoints[i]);
            checkpointLabels = File.ReadAllLines("CheckpointLabels.txt");
        }
    }
}
