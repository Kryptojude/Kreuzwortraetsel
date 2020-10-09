using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace Kreuzworträtsel
{
    public partial class Form1 : Form
    {
        Random random = new Random();
        /// <summary>
        /// tilesize
        /// </summary>
        int ts = 25;
        string[,] grid = new string[16, 16];
        List<(string Question, string Answer)> database = new List<(string, string)>();
        int databaseIndex = 0;
        int questionCounter = 0;
        /// <summary>
        /// Possible offsets for horizontal pointing question tiles
        /// </summary>
        // TODO: offset probabilities are fucked because an offset for a top row tile to the left is impossible, since the loop goes from left to right, so the left neighbor tile is always occupied already
        static Point[] horizontalOffsets = new Point[5] { new Point(-1, -1), new Point(-1, -1), new Point(0, 0), new Point(-1, 1), new Point(-1, 1) };
        static Point[] verticalOffsets = new Point[5] { new Point(-1, -1), new Point(-1, -1), new Point(0, 0), new Point(1, -1), new Point(1, -1) };
        //static Point[] horizontalOffsets = new Point[3] { new Point(-1, -1), new Point(0, 0), new Point(-1, 1) };
        //static Point[] verticalOffsets = new Point[3] { new Point(-1, -1), new Point(0, 0), new Point(1, -1) };
        Dictionary<int, Point[]> directionToOffsetTranslator = new Dictionary<int, Point[]>() { {0, verticalOffsets}, {1, horizontalOffsets} };
        int offsetIndex = 0;
        List<Point> reservedTiles = new List<Point>();
        Image dirt = Image.FromFile("dirt.png");

        // TODO: implement offset arrows
        public Form1()
        {
            InitializeComponent();
            //Adjust window size to the grid
            Width = grid.GetLength(1) * ts + 16;
            Height = grid.GetLength(0) * ts + 39;
            BackgroundImage = dirt;
            DoubleBuffered = true;
            Resize += (s, e) => { SuspendLayout(); };
            ResizeEnd += Form1_Resize;

            // Fetch database from file
            StreamReader reader = new StreamReader("databaseDeutsch.txt");
            string line = reader.ReadLine();
            while (line != null)
            {
                string question = line.Substring(0, line.IndexOf(';'));
                string answer = line.Substring(line.IndexOf(';') + 1);
                database.Add((question, answer));
                line = reader.ReadLine();
            }
            
            ScrambleDatabase();

            // Go through each edge tile
            // Top and bottom row
            for (int y = 0; y < grid.GetLength(0); y += grid.GetUpperBound(0))
            {
                for (int x = 0; x < grid.GetLength(1); x++)
                {
                    // If tile is empty (no question tile, no letter, not blocked, not reserved)
                    if (grid[y, x] == null)
                    {
                        DetermineDirectionAndOffset(x, y, reserveEndTile: true);
                    }
                }
            }
            // Left and right column // TODO: split into 4 loops, so that right and bottom don't reserve tiles
            for (int x = 0; x < grid.GetLength(0); x += grid.GetUpperBound(0))
            {
                for (int y = 1; y < grid.GetLength(1) - 1; y++)
                {
                    // If tile is empty (no question tile, no letter, not blocked, not reserved)
                    if (grid[y, x] == null)
                    {
                        DetermineDirectionAndOffset(x, y, reserveEndTile: true);
                    }
                }
            }

            //Fill all the tiles that were "reserved" in the last two loops
            foreach (Point p in reservedTiles)
            {
                DetermineDirectionAndOffset(p.X, p.Y, reserveEndTile: false);
            }

            // Go through each main tile
            for (int y = 1; y < grid.GetLength(0) - 1; y++)
            {
                for (int x = 1; x < grid.GetLength(1) - 1; x++)
                {
                    // If tile is empty (no question tile, no letter, not blocked, not reserved)
                    if (grid[y, x] == null)
                    {
                        DetermineDirectionAndOffset(x, y, reserveEndTile: false);
                    }
                }
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            // Increase tile size with window size
            ts = (Width - 16) / grid.GetLength(0);
            // Tie width to height
            Height = Width - 16 + 39;
            ResumeLayout(true);
        }

        /// <summary>
        /// Fills the top, left, bottom and right edges of the grid
        /// </summary>
        /// <param name="reserveEndTile">if true, then after the answer has been filled in, the tile after that will be reserved for later instead of being filled with question tile</param>
        private void DetermineDirectionAndOffset(int x, int y, bool reserveEndTile)
        {
            Show();
            //Refresh();

            // Determine direction of the text, edges have to have certain orientation
            Point direction = new Point(0, 0);
            Point offset = new Point(0, 0);
            bool directionLocked = false;
            bool offsetLocked = false;
            ScrambleOffsets();
            offsetIndex = 0;

            // Bottom row
            if (y == grid.GetUpperBound(0))
            {
                SetDirection("horizontal", ref direction);
                directionLocked = true;
                offsetLocked = true;
            }
            // Right column
            else if (x == grid.GetUpperBound(1))
            {
                SetDirection("vertical", ref direction);
                directionLocked = true;
                offsetLocked = true;
            }
            // Top row
            else if (y == 0)
            {
                SetDirection("vertical", ref direction);
                offset = verticalOffsets[offsetIndex];
                directionLocked = true;
            }
            // Left column
            else if (x == 0)
            {
                SetDirection("horizontal", ref direction);
                offset = horizontalOffsets[offsetIndex];
                directionLocked = true;
            }
            // Main tile (not on any edge)
            else
            {
                SetDirection("random", ref direction);
                offsetLocked = true;
            }

            // Handle bottom right corner: Can't put question tile in here
            if (y == grid.GetUpperBound(0) && x == grid.GetUpperBound(1))
                grid[grid.GetUpperBound(0), grid.GetUpperBound(1)] = "blocked";
            // Bottom left corner:  Bottom and left edges are both horizontal
            // Top right corner:    Top and right edges are both vertical
            // Top left corner:     Vertical takes precedence in if/else edge check, but make it random since it can be both ways with proper offset                  
            if (y == 0 && x == 0)
            {
                SetDirection("random", ref direction);

                if (direction.X == 1)
                {
                    offset.X = -1;
                    offset.Y = 1;
                }
                else
                {
                    offset.X = 1;
                    offset.Y = -1;
                }

                directionLocked = true; // Not really relevant since top left corner is checked first, so it should never not find a fitting word
                offsetLocked = true;
            }

            FillAnswer(direction, offset, directionLocked, offsetLocked, x, y, reserveEndTile);
        }

        private void FillAnswer(Point direction, Point offset, bool directionLocked, bool offsetLocked, int x, int y, bool reserveEndTile)
        {
            int directionsTested = 0;
            (string Question, string Answer) databaseEntry = ("", "");
            // Determine maximum length and what the answer has to match
            bool wordFound = false;
            // First loop: cycle through directions
            do
            {
                // Second loop: cycle through offsets
                do
                {
                    // EDGE OFFSET FIX: there are supposed to be three configurations saved in horizontalOffsets, but the (-1, -1) point can never be applied cause the left neighbour tile
                    // will always have question tile in it already, so this question tile has to be moved right or down
                    // Check if answer starting tile is out of bounds
                    if (!offsetLocked)
                        if (y + direction.Y + offset.Y >= grid.GetLowerBound(0) && y + direction.Y + offset.Y <= grid.GetUpperBound(0) &&
                            x + direction.X + offset.X >= grid.GetLowerBound(1) && x + direction.X + offset.X <= grid.GetUpperBound(1)  )
                        {
                            // Checks if answer starting tile is used by questionTile
                            if (grid[y + direction.Y + offset.Y, x + direction.X + offset.X]?.Contains("►") == true ||
                                grid[y + direction.Y + offset.Y, x + direction.X + offset.X]?.Contains("▼") == true)
                            {
                                // Then move the question tile to make it work anyway with given offset
                                // But check if new coordinates are in bounds
                                if (x + direction.Y >= grid.GetLowerBound(1) && x + direction.Y <= grid.GetUpperBound(1) ||
                                    y + direction.X >= grid.GetLowerBound(0) && y + direction.X <= grid.GetUpperBound(0) )
                                {
                                    x += direction.Y;
                                    y += direction.X;
                                }
                            }
                        }
                    // This loop goes along the path of the potential answer till it hits something and saves what an answer has to match in order to fit in here
                    string toBeMatched = "";
                    while (true) // TODO: replace true with the out of bounds condition, get rid of breaks I guess
                    {
                        // Get current coordinate
                        Point p = new Point();
                        p.X = x + (direction.X * (toBeMatched.Length + 1) + offset.X); // (x,y) is question tile, so +1 for start of answer
                        p.Y = y + (direction.Y * (toBeMatched.Length + 1) + offset.Y);

                        // Out of bounds check
                        if (p.Y > grid.GetUpperBound(0) || p.Y < grid.GetLowerBound(0) || 
                            p.X > grid.GetUpperBound(1) || p.X < grid.GetLowerBound(1))
                            break;
                        // Empty tile check
                        else if (grid[p.Y, p.X] == null)
                            toBeMatched += " ";
                        // Question tile / blocked tile check
                        else if (grid[p.Y, p.X].Contains("►") ||
                                 grid[p.Y, p.X].Contains("▼") ||
                                 grid[p.Y, p.X] == "blocked"  ||
                                 grid[p.Y, p.X] == "reserved")
                                    break;
                        // Must be letter tile, bc not question tile/blocked and not null
                        else
                        {
                            if (grid[p.Y, p.X].Length > 1)
                                throw new Exception("supposed letter tile contained more than one letter");
                            // Add that letter
                            toBeMatched += grid[p.Y, p.X];
                        }
                    }

                    // Loop through fetch attempts
                    string answer = "";
                    int attempts = 0;
                    while (!wordFound && attempts < database.Count)
                    {
                        wordFound = true;

                        databaseEntry = FetchAnswer();
                        answer = databaseEntry.Answer;
                        // Answer is longer than toBeMatched
                        if (answer.Length > toBeMatched.Length)
                        {
                            wordFound = false;
                        }
                        // Answer is shorter or equal to toBeMatched
                        else
                        {
                            // Check match with toBeMatched string
                            for (int i = 0; i < answer.Length; i++)
                            {
                                if (toBeMatched[i] != ' ' && answer[i] != toBeMatched[i])
                                    wordFound = false;
                            }
                            // if answer is shorter than toBeMatched,
                            // then there has to be a space after the answer
                            if (answer.Length < toBeMatched.Length)
                                if (toBeMatched[answer.Length] != ' ')
                                    wordFound = false;
                        }

                        attempts++;
                    }

                    // Cycle the offset if appropriate
                    if (!wordFound && !offsetLocked)
                    {
                        offset = directionToOffsetTranslator[direction.X][offsetIndex++];
                        if (offsetIndex == horizontalOffsets.Length)
                            offsetLocked = true;
                    }
                }
                while (!wordFound && !offsetLocked);

                // Cycle the direction if appropriate
                if (!wordFound && !directionLocked)
                {
                    SetDirection("swap", ref direction); // TODO: replace ref with return in this function
                    directionsTested++;
                }
            }
            while (!wordFound && !directionLocked && directionsTested < 2);

            // If wordFound is true, then the loops were exited because a word was found
            // so fill the word into the grid
            if (wordFound)
            {
                // Fill the question indicator into the tile
                string arrow = (direction.X == 1) ? "►" : "\n▼";
                grid[y, x] = (questionCounter + 1) + arrow;

                int letterX = 0; // Absolute values
                int letterY = 0;
                // Fill the answer into the grid letter by letter
                for (int c = 0; c < databaseEntry.Answer.Length; c++)
                {
                    letterX = x + (direction.X * (c + 1) + offset.X);
                    letterY = y + (direction.Y * (c + 1) + offset.Y);
                    grid[letterY, letterX] = databaseEntry.Answer[c].ToString();
                    //Refresh();
                }

                // Remove that question/answer from database
                database.Remove(databaseEntry);
                if (databaseIndex >= database.Count)
                    databaseIndex = 0;

                questionCounter++;
                // In bounds check for next question tile
                if (letterY + direction.Y < grid.GetLength(0) && letterX + direction.X < grid.GetLength(1))
                    // Empty tile check for next question tile
                    if (grid[letterY + direction.Y, letterX + direction.X] == null)
                        // reserveEndTile controls if the recursive function calling continues
                        // this is the regular branch that is followed, if this function was called from a main tile
                        if (!reserveEndTile)
                            DetermineDirectionAndOffset(letterX + direction.X, letterY + direction.Y, reserveEndTile: false);
                        // If this was called from the two edge filling loops (reserveEndTile = true), then don't continue the recursive function calling 
                        else
                        {
                            grid[letterY + direction.Y, letterX + direction.X] = "reserved";
                            reservedTiles.Add(new Point(letterX + direction.X, letterY + direction.Y));
                        }
                Debug.WriteLine(offset.X + " " + offset.Y);
            }
            // if wordFound is false, then the loops were exited not because a word was found, but because it iterated through all offsets and/or directions till there was nothing left to cycle through
            else
                grid[y, x] = "blocked"; // Block the tile from getting question tile and letter from an answer
        }

        private void SetDirection(string directionMode, ref Point direction)
        {
            switch (directionMode)
            {
                case "horizontal":
                    direction = new Point(1, 0);
                    break;
                case "vertical":
                    direction = new Point(0, 1);
                    break;
                case "random":
                    if (random.Next(2) == 0)
                        direction = new Point(1, 0);
                    else
                        direction = new Point(0, 1);
                    break;
                case "swap":
                    if (direction.X == 1)
                        direction = new Point(0, 1);
                    else
                        direction = new Point(1, 0);
                    break;
            }
        }

        private void ScrambleOffsets()
        {
            Point[] horizontalBuffer = new Point[horizontalOffsets.Length];
            Point[] verticalBuffer = new Point[verticalOffsets.Length];
            List<int> filledSpots = new List<int>();
            for (int i = 0; i < horizontalOffsets.Length; i++)
            {
                // Find random index that's still empty
                while (true)
                {
                    int index = random.Next(horizontalBuffer.Length);
                    if (!filledSpots.Contains(index))
                    {
                        horizontalBuffer[index] = horizontalOffsets[i];
                        verticalBuffer[index] = verticalOffsets[i];
                        filledSpots.Add(index);
                        break;
                    }
                }
            }

            horizontalOffsets = horizontalBuffer;
            verticalOffsets = verticalBuffer;
        }

        /// <summary>
        /// Returns next question/answer tuple, increments databaseIndex
        /// </summary>
        private (string, string) FetchAnswer()
        {
            (string, string Answer) tuple = database[databaseIndex];

            databaseIndex++;
            if (databaseIndex >= database.Count)
                databaseIndex = 0;

            return tuple;
        }

        /// <summary>
        /// Randomizes the order of the question/answer pairs
        /// </summary>
        private void ScrambleDatabase()
        {
            List<(string Question, string Answer)> database2 = new List<(string Question, string Answer)>();
            for (int i = 0; i < database.Count; i++)
            {
                database2.Add(("", ""));
            }
            
            for (int i = 0; i < database.Count; i++)
            {
                while (true)
                {
                    int randomSpot = random.Next(database2.Count);
                    if (database2[randomSpot].Answer == "")
                    {
                        database2[randomSpot] = database[i];
                        break;
                    }
                }
            }

            database = database2;
        }

        // TODO: try calling this method to turn the call on and off based on if the user is resizing or not
        protected override void OnPaint(PaintEventArgs e)
        {
            for (int y = 0; y < grid.GetLength(0); y++)
            {
                for (int x = 0; x < grid.GetLength(1); x++)
                {
                    if (grid[y, x] != null)
                    {
                        Size textSize = TextRenderer.MeasureText(grid[y, x], Font);
                        if (grid[y, x].Contains("►") ||
                            grid[y, x].Contains("▼")   )
                        { // question tile
                            e.Graphics.DrawRectangle(Pens.Black, x * ts, y * ts, ts, ts);
                            e.Graphics.DrawString(grid[y, x], Font, Brushes.Red, x * ts + ts/2 - textSize.Width/2, y * ts + ts / 2 - textSize.Height / 2);
                        }
                        else if (grid[y, x] == "blocked")
                        { // blocked tile
                            e.Graphics.FillRectangle(Brushes.Black, x * ts, y * ts, ts, ts);
                        }
                        else
                        { // letter tile
                            e.Graphics.DrawRectangle(Pens.Black, x * ts, y * ts, ts, ts);
                            //e.Graphics.DrawString(grid[y, x], Font, Brushes.DarkBlue, x * ts + ts / 2 - textSize.Width / 2, y * ts + ts / 2 - textSize.Height / 2);
                        }
                    }
                }
            }
        }
    }
}