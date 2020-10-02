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

namespace Kreuzworträtsel
{
    public partial class Form1 : Form
    {
        Random random = new Random();
        /// <summary>
        /// tilesize
        /// </summary>
        int ts = 25;
        string[,] grid = new string[20, 20];
        List<(string Question, string Answer)> database = new List<(string, string)>();
        int databaseIndex = 0;
        int questionCounter = 0;
        /// <summary>
        /// Possible offsets for horizontal pointing question tiles
        /// </summary>
        Point[] horizontalOffsets = new Point[3] { new Point(-1, -1), new Point(0, 0), new Point(-1, 1) };
        Point[] verticalOffsets = new Point[3] { new Point(-1, -1), new Point(0, 0), new Point(1, -1) };

        public Form1()
        {
            InitializeComponent();
            //Adjust window size to the grid
            Width = grid.GetLength(1) * ts + 16;
            Height = grid.GetLength(0) * ts + 39;

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
                    // If tile is empty (no question tile, no letter, not blocked)
                    if (grid[y, x] == null)
                    {
                        DetermineDirectionAndOffset(x, y);
                    }
                }
            }
            // Left and right column
            for (int x = 0; x < grid.GetLength(0); x += grid.GetUpperBound(0))
            {
                for (int y = 1; y < grid.GetLength(1) - 1; y++)
                {
                    // If tile is empty (no question tile, no letter, not blocked)
                    if (grid[y, x] == null)
                    {
                        DetermineDirectionAndOffset(x, y);
                    }
                }
            }
            
            // Go through each main tile
            for (int y = 1; y < grid.GetLength(0) - 1; y++)
            {
                for (int x = 1; x < grid.GetLength(1) - 1; x++)
                {
                    // If tile is empty (no question tile, no letter, not blocked)
                    if (grid[y, x] == null)
                    {
                        DetermineDirectionAndOffset(x, y);
                    }
                }
            }
        }
        /// <summary>
        /// Fills the top, left, bottom and right edges of the grid
        /// </summary>
        private void DetermineDirectionAndOffset(int x, int y)
        {
            Show();
            Refresh();

            // Determine direction of the text, edges have to have certain orientation
            Point direction = new Point(0, 0);
            Point offset = new Point(0, 0);
            bool directionLocked = false;
            bool offsetLocked = false;
            ScrambleOffsets();

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
                offset = verticalOffsets[0];
                directionLocked = true;
            }
            // Left column
            else if (x == 0)
            {
                SetDirection("horizontal", ref direction);
                offset = horizontalOffsets[0];
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

            // TODO: continue with FillAnswer, but then don't continue with filling the main tiles, finish all edge tiles first, reserve the main question tiles that end the generated answers
            FillAnswer(direction, offset, directionLocked, offsetLocked, x, y);
        }

        private void FillAnswer(Point direction, Point offset, bool directionLocked, bool offsetLocked, int x, int y)
        {
            int directionsTested = 0;
            int offsetsTested = 0;
            (string Question, string Answer) databaseEntry = ("", "");
            // Determine maximum length and what the answer has to match
            bool wordFound = false;
            while (!wordFound)
            {
                wordFound = true;
                string toBeMatched = "";
                while (true)
                {
                    // Get current coordinate
                    Point p = new Point();
                    p.X = x + (direction.X * (toBeMatched.Length + 1) + offset.X); // (x,y) is question tile, so +1 for start of answer
                    p.Y = y + (direction.Y * (toBeMatched.Length + 1) + offset.Y);

                    // Out of bounds check
                    if (p.Y > grid.GetUpperBound(0) || p.X > grid.GetUpperBound(1))
                        break;
                    // Empty tile check
                    else if (grid[p.Y, p.X] == null)
                        toBeMatched += " ";
                    // Question tile / blocked tile check
                    else if (grid[p.Y, p.X].Contains("►") ||
                             grid[p.Y, p.X].Contains("▼") ||
                             grid[p.Y, p.X] == "blocked")
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
                bool error = true;
                int attempt = 0;
                while (error)
                {
                    error = false;

                    databaseEntry = FetchAnswer();
                    answer = databaseEntry.Answer;
                    // Check if answer would fit
                    if (answer.Length > toBeMatched.Length)
                        error = true;
                    else
                    {
                        // check match with toBeMatched string
                        for (int i = 0; i < answer.Length; i++)
                        {
                            if (toBeMatched[i] != ' ' && answer[i] != toBeMatched[i])
                                error = true;
                        }
                    }

                    // if answer is shorter than toBeMatched,
                    // then there has to be a space after the answer
                    if (toBeMatched.Length > answer.Length)
                        if (toBeMatched[answer.Length] != ' ')
                            error = true;

                    attempt++;

                    // Exit fetch loop?
                    if (attempt == database.Count) // No possible word exists
                    {
                        error = false;
                        wordFound = false;
                        // Retry upper loop with different conditions
                        if (!directionLocked)
                        {
                            SetDirection("swap", ref direction);
                            directionLocked = true;
                        }
                        else if ()
                    }
                }
            }
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
            // in bounds check for next question tile
            if (letterY + direction.Y < grid.GetLength(0) && letterX + direction.X < grid.GetLength(1))
                // empty tile check for next question tile
                if (grid[letterY + direction.Y, letterX + direction.X] == null)
                    DetermineDirectionAndOffset(letterX + direction.X, letterY + direction.Y);
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

        //private void SetOffset(ref Point offset, Point direction, int x, int y)
        //{
        //    // Offset at all?
        //    int percentChance = 66;
        //    if (random.Next(1, 101) <= percentChance)
        //    {
        //        // Determine offset based on direction
        //        int r = random.Next(2);
        //        switch (direction.X)
        //        {
        //            case 1: // horizontal
        //                offset.X = -1;
        //                offset.Y = (r == 0) ? 1 : -1;
        //            break;
        //            case 0: // vertical
        //                offset.X = (r == 0) ? 1 : -1;
        //                offset.Y = -1;
        //            break;
        //        }

        //        // Out of bounds check
        //        // Get coordinate of first letter
        //        Point p = new Point();
        //        p.X = x + direction.X + offset.X; // x, y is question tile
        //        p.Y = y + direction.Y + offset.Y;
        //        if (p.X < grid.GetLowerBound(1) || p.X > grid.GetUpperBound(1) ||
        //            p.Y < grid.GetLowerBound(0) || p.Y > grid.GetUpperBound(0))
        //            offset = new Point(0, 0);
        //    }
        //}

        private void ScrambleOffsets()
        {
            Point[] horizontalBuffer = new Point[horizontalOffsets.Length];
            Point[] verticalBuffer = new Point[verticalOffsets.Length];
            for (int i = 0; i < horizontalOffsets.Length; i++)
            {
                // Find random index that's still empty
                while (true)
                {
                    int index = random.Next(horizontalBuffer.Length);
                    if (horizontalBuffer[index] == null)
                    {
                        horizontalBuffer[index] = horizontalOffsets[i];
                        verticalBuffer[index] = verticalOffsets[i];
                        break;
                    }
                }
            }

            horizontalOffsets = horizontalBuffer;
            verticalOffsets = verticalBuffer;
        }

        private void CycleOffset(List<int> offsetsTested, ref Point offset, Point direction)
        {
            List<int> indeces = new List<int> { 0, 1, 2 }; // TODO: Unfuck (indices get reset, so it's useless)
            // Get offsets that haven't been tried
            for (int i = 0; i < offsetsTested.Count(); i++)
            {
                if (offsetsTested[i] == indeces[i])
                {
                    indeces.Remove(indeces[i]);
                    break;
                }
            }

            if (direction.X == 1)
                offset = horizontalOffsets[random.Next(indeces.Count())];
            else
                offset = verticalOffsets[random.Next(indeces.Count())];
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
            // Remember which spots in database2 have been filled:
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
                            e.Graphics.FillRectangle(Brushes.Gray, x * ts, y * ts, ts, ts);
                        }
                        else
                        { // letter tile
                            e.Graphics.DrawRectangle(Pens.Black, x * ts, y * ts, ts, ts);
                            e.Graphics.DrawString(grid[y, x], Font, Brushes.DarkBlue, x * ts + ts / 2 - textSize.Width / 2, y * ts + ts / 2 - textSize.Height / 2);
                        }
                    }
                }
            }
        }
    }
}