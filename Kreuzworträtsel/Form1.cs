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

            // Block bottom right tile from having question tile in it
            grid[grid.GetUpperBound(0), grid.GetUpperBound(1)] = "blocked";

            // Go through each edge tile
            int questionCounter = 0;
            for (int y = 0; y < grid.GetLength(0); y += grid.GetUpperBound(0))
            {
                for (int x = 0; x < grid.GetLength(1); x += grid.GetUpperBound(1))
                {
                    // If tile is empty (no question tile, no letter, not blocked)
                    if (grid[y, x] == null)
                    {
                        FillQuestionEdges(ref questionCounter, x, y);
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
                        FillQuestionMain(ref questionCounter, x, y);
                    }
                }
            }
        }
        /// <summary>
        /// Fills the top, left, bottom and right edges of the grid
        /// </summary>
        private void FillQuestionEdges(ref int questionCounter, int x, int y)
        {
            Show();
            //Refresh();

            // Determine direction of the text, edges have to have certain orientation
            (int horizontal, int vertical) direction = (0, 0);
            bool directionLocked = false;
            Point offset = new Point(0, 0);

            // Bottom row
            if (y == grid.GetUpperBound(0))
            {
                SetDirection("horizontal", ref direction);
                directionLocked = true;
            }
            // Right column
            else if (x == grid.GetUpperBound(1))
            {
                SetDirection("vertical", ref direction);
                directionLocked = true;
            }
            // Top row
            else if (y == 0)
            {
                SetDirection("vertical", ref direction);
                SetOffset(ref offset, direction, x, y);
                directionLocked = true;
            }
            // Left column
            else if (x == 0)
            {
                SetDirection("horizontal", ref direction);
                SetOffset(ref offset, direction, x, y);
                directionLocked = true;
            }
            else
                throw new Exception("this function was called for the wrong tile, should be edge tile but wasn't");

            // Top left corner special case
            // Overwrite direction and offset

            // Bottom right corner: Has "blocked" value
            // Bottom left corner:  Bottom and left edges are both horizontal
            // Top right corner:    Top and right edges are both vertical
            // Top left corner:     Vertical takes precedence in if/else edge check, 
            //                      but make it random since it can be both ways with offset
            if (y == 0 && x == 0)
            {
                SetDirection("random", ref direction);

                if (direction.horizontal == 1)
                {
                    offset.X = -1;
                    offset.Y = 1;
                }
                else
                {
                    offset.X = 1;
                    offset.Y = -1;
                }
            }

            FillAnswer(direction, offset, directionLocked, questionCounter, x, y);
        }

        private void FillQuestionMain(ref int questionCounter, int x, int y)
        {
            Show();
            //Refresh();

            // Determine direction of the text
            (int horizontal, int vertical) direction = (0,0);
            bool directionLocked = false;
            Point offset = new Point(0,0);

            SetDirection("random", ref direction);

            FillAnswer(direction, offset, directionLocked, questionCounter, x, y);
        }

        private void FillAnswer((int horizontal, int vertical) direction, Point offset, bool directionLocked, int questionCounter, int x, int y)
        {
            int directionsTested = 0;
            retryAfterDirectionChange:
            // Determine maximum length and what the answer has to match
            string toBeMatched = "";
            while (true)
            {
                // Get current coordinate
                Point p = new Point();
                p.X = x + (direction.horizontal * (toBeMatched.Length + 1) + offset.X); // (x,y) is question tile, so +1 for start of answer
                p.Y = y + (direction.vertical * (toBeMatched.Length + 1) + offset.Y);

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

            // Fetch answer with correct length and letter match
            (string Question, string Answer) databaseEntry = ("", "");
            string answer = "";
            bool error = true;
            int attempt = 0;
            while (error)
            {
                error = false;
                if (attempt == database.Count) // No possible word exists
                {
                    // Try other direction
                    directionsTested++;
                    SetDirection("swap", ref direction);
                    if (directionsTested == 1 && !directionLocked)
                        goto retryAfterDirectionChange;
                    else
                    {
                        grid[y, x] = "blocked";
                        return;
                    }
                }

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
            }

            // Fill the question indicator into the tile
            string arrow = (direction.horizontal == 1) ? "►" : "\n▼";
            grid[y, x] = (questionCounter + 1) + arrow;

            int letterX = 0; // Absolute values
            int letterY = 0;
            // Fill the answer into the grid letter by letter
            for (int c = 0; c < answer.Length; c++)
            {
                letterX = x + (direction.horizontal * (c + 1) + offset.X);
                letterY = y + (direction.vertical * (c + 1) + offset.Y);
                grid[letterY, letterX] = answer[c].ToString();
                //Refresh();
            }

            // Remove that question/answer from database
            database.Remove(databaseEntry);
            if (databaseIndex >= database.Count)
                databaseIndex = 0;

            questionCounter++;
            // in bounds check for next question tile
            if (letterY + direction.vertical < grid.GetLength(0) && letterX + direction.horizontal < grid.GetLength(1))
                // empty tile check for next question tile
                if (grid[letterY + direction.vertical, letterX + direction.horizontal] == null)
                    FillQuestionMain(ref questionCounter, letterX + direction.horizontal, letterY + direction.vertical);
        }

        private void SetDirection(string directionMode, ref (int horizontal, int vertical) direction)
        {
            switch (directionMode)
            {
                case "horizontal":
                    direction = (1, 0);
                    break;
                case "vertical":
                    direction = (0, 1);
                    break;
                case "random":
                    if (random.Next(2) == 0)
                        direction = (1, 0);
                    else
                        direction = (0, 1);
                    break;
                case "swap":
                    if (direction.horizontal == 1)
                        direction = (0, 1);
                    else
                        direction = (1, 0);
                    break;
            }

        }

        private void SetOffset(ref Point offset, (int horizontal, int vertical) direction, int x, int y)
        {
            // Offset at all?
            int percentChance = 50;
            if (random.Next(100/percentChance) == 1)
            {
                // Determine offset based on direction
                int r = random.Next(2);
                switch (direction.horizontal)
                {
                    case 1: // horizontal
                        offset.X = -1;
                        offset.Y = (r == 0) ? 1 : -1;
                    break;
                    case 0: // vertical
                        offset.X = (r == 0) ? 1 : -1;
                        offset.Y = -1;
                        break;
                }

                // Out of bounds check
                // Get coordinate of first letter
                Point p = new Point();
                p.X = x + direction.horizontal + offset.X; // x, y is question tile
                p.Y = y + direction.vertical + offset.Y;
                if (p.X < grid.GetLowerBound(1) || p.X > grid.GetUpperBound(1) ||
                    p.Y < grid.GetLowerBound(0) || p.Y > grid.GetUpperBound(0))
                    offset = new Point(0, 0);
            }
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
            int[] filledSpots = new int[database2.Count];
            for (int i = 0; i < database.Count; i++)
            {
                while (true)
                {
                    int randomSpot = random.Next(database2.Count);
                    if (filledSpots[randomSpot] == 0)
                    {
                        database2[randomSpot] = database[i];
                        filledSpots[randomSpot] = 1;
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