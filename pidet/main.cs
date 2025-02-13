﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace Pidet
{
    public partial class Main : Form
    {
        #region Definitions

        const string FILE_FORMAT_FILTER = "Image|*.png;*.bmp|PNG|*.png|Bitmap|*.bmp";
        
        List<List<int>> codels = new List<List<int>>(), buffer = new List<List<int>>();
        List<Comment> comments;
        List<List<bool>> breakPoints = new List<List<bool>>();
        List<List<List<int>>> history = new List<List<List<int>>>();
        int codelSize = 20, fieldWidth = 3, fieldHeight = 3, historyIndex = -1, currentColor = 0, standardColor = 0;
        Mode editMode = Mode.PEN_MODE;
        bool penWriting = false;

        int fileCodelSize = 10;
        string fileName = "NoName", filePath = "";
        bool saveRequired = false;

        int directionPointer, codelChooser, waitCount, stepCount, slideCount;
        Point currentCodel, nextCodel;
        List<Point> slideCodels;
        string inputStr, inputStrTmp, outputStr, currentCommand;
        bool inputRequired = false, paused = true;
        List<long> stack;
        List<List<int>> colorBlockSizes = new List<List<int>>();
        List<List<List<List<int>>>> corners = new List<List<List<List<int>>>>(); //各codelのRDLUの端の{x, y(L), y(R)} or {y, x(L), x(R)}
        string[] commandNames = 
            { "*", "push", "pop", "add", "sub", "multi", "div", "mod", "not",
                "great", "point", "switch", "dup", "roll", "in(n)", "in(c)", "out(n)", "out(c)" },
                directionPointerStrs = { "→(0)", "↓(1)", "←(2)", "↑(3)" },
                codelChooserStrs = { "L(0)", "R(1)" };

        enum Mode
        {
            PEN_MODE,
            SELECTOR_MODE,
            DEBUG_MODE
        }

        #endregion

        void ChangeEditMode(Mode e)
        {
            editMode = e;
            if (e == Mode.PEN_MODE)
            {
                //dgv_field.Enabled = true;
                btn_change.Enabled = true;
                btn_reset.Enabled = false;
                dgv_field.Cursor = Cursors.Hand;
                dgv_field.MultiSelect = false;
                dgv_field.Refresh();
                lbl_status.BackColor = Color.Lavender;
            }
            else if (e == Mode.SELECTOR_MODE)
            {
                //dgv_field.Enabled = true;
                btn_change.Enabled = true;
                btn_reset.Enabled = false;
                dgv_field.Cursor = Cursors.Cross;
                dgv_field.MultiSelect = true;
                dgv_field.Refresh();
                lbl_status.BackColor = Color.Lavender;
            }
            else
            {
                //dgv_field.Enabled = false;
                btn_reset.Enabled = true;
                btn_change.Enabled = false;
                dgv_field.Cursor = Cursors.Default;
                lbl_status.BackColor = Color.LavenderBlush;
            }
        }

        void ToggleEditMode()
        {
            if (editMode == Mode.DEBUG_MODE)
            {
                // throw Exception;
                return;
            }

            if (dgv_field.MultiSelect)
            {
                ChangeEditMode(Mode.PEN_MODE);
            }
            else
            {
                ChangeEditMode(Mode.SELECTOR_MODE);
            }
        }

        void EndDebugMode()
        {
            if (dgv_field.MultiSelect)
            {
                ChangeEditMode(Mode.SELECTOR_MODE);
            }
            else
            {
                ChangeEditMode(Mode.PEN_MODE);
            }
        }

        #region Debug

        string ReplaceCrLf(string e, bool istoLF = true)
        {
            if (istoLF) return e.Replace("\r\n", "\n");
            else return e.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        void PrepareDebug()
        {
            ChangeEditMode(Mode.DEBUG_MODE);
            currentCodel = new Point(0, 0);
            nextCodel = new Point(0, 0);
            slideCodels = new List<Point>();
            directionPointer = codelChooser = waitCount = stepCount = slideCount = 0;
            inputStr = inputStrTmp = ReplaceCrLf(tb_input.Text);
            outputStr = currentCommand = "";
            inputRequired = false;
            stack = new List<long>();
            colorBlockSizes.Clear();
            corners.Clear();
            for (int i = 0; i < fieldWidth; i++)
            {
                colorBlockSizes.Add(new List<int>());
                corners.Add(new List<List<List<int>>>());
                for (int j = 0; j < fieldHeight; j++)
			    {
                    colorBlockSizes[i].Add(0);
                    corners[i].Add(new List<List<int>>());
			    }
            }
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            #region algo1
            for (int i = 0; i < fieldWidth; i++)
            {
                for (int j = 0; j < fieldHeight; j++)
                {
                    if (corners[i][j].Count > 0) continue;
                    if (codels[i][j] == PietColors.White || codels[i][j] == PietColors.Black) continue;
                    List<Point> colorBlockTemp = new List<Point> { new Point(i, j) },
                        colorBlockNew = new List<Point>();
                    List<List<int>> cornerTemp = new List<List<int>>();
                    for (int k = 0; k < 2; k++)
                    {
                        cornerTemp.Add(new List<int> { i, j, j });
                        cornerTemp.Add(new List<int> { j, i, i });
                    }
                    while (colorBlockTemp.Count > 0)
                    {
                        int codelX = colorBlockTemp[0].X, codelY = colorBlockTemp[0].Y;
                        if ((codelX > -1 && codelX < fieldWidth && codelY > -1 && codelY < fieldHeight)
                            && (!colorBlockNew.Contains(new Point(codelX, codelY))
                            && codels[codelX][codelY] == codels[i][j]))
                        {
                            colorBlockNew.Add(new Point(codelX, codelY));
                            if (codelX == cornerTemp[0][0]) //R
                            {
                                cornerTemp[0][0] = codelX;
                                if (codelY < cornerTemp[0][1]) cornerTemp[0][1] = codelY;
                                if (codelY > cornerTemp[0][2]) cornerTemp[0][2] = codelY;
                            }
                            else if (codelX > cornerTemp[0][0])
                            {
                                cornerTemp[0][0] = codelX;
                                cornerTemp[0][1] = cornerTemp[0][2] = codelY;
                            }

                            if (codelY == cornerTemp[1][0]) //D
                            {
                                cornerTemp[1][0] = codelY;
                                if (codelX > cornerTemp[1][1]) cornerTemp[1][1] = codelX;
                                if (codelX < cornerTemp[1][2]) cornerTemp[1][2] = codelX;
                            }
                            else if (codelY > cornerTemp[1][0])
                            {
                                cornerTemp[1][0] = codelY;
                                cornerTemp[1][1] = cornerTemp[1][2] = codelX;
                            }

                            if (codelX == cornerTemp[2][0]) //L
                            {
                                cornerTemp[2][0] = codelX;
                                if (codelY > cornerTemp[2][1]) cornerTemp[2][1] = codelY;
                                if (codelY < cornerTemp[2][2]) cornerTemp[2][2] = codelY;
                            }
                            else if (codelX < cornerTemp[2][0])
                            {
                                cornerTemp[2][0] = codelX;
                                cornerTemp[2][1] = cornerTemp[2][2] = codelY;
                            }

                            if (codelY == cornerTemp[3][0]) //U
                            {
                                cornerTemp[3][0] = codelY;
                                if (codelX < cornerTemp[3][1]) cornerTemp[3][1] = codelX;
                                if (codelX > cornerTemp[3][2]) cornerTemp[3][2] = codelX;
                            }
                            else if (codelY < cornerTemp[3][0])
                            {
                                cornerTemp[3][0] = codelY;
                                cornerTemp[3][1] = cornerTemp[3][2] = codelX;
                            }

                            colorBlockTemp.Add(new Point(codelX - 1, codelY));
                            colorBlockTemp.Add(new Point(codelX + 1, codelY));
                            colorBlockTemp.Add(new Point(codelX, codelY - 1));
                            colorBlockTemp.Add(new Point(codelX, codelY + 1));
                        }
                        colorBlockTemp.RemoveAt(0);
                    }
                    foreach (Point item in colorBlockNew)
                    {
                        colorBlockSizes[item.X][item.Y] = colorBlockNew.Count;
                        for (int k = 0; k < 4; k++)
                        {
                            corners[item.X][item.Y].Add(new List<int>());
                            for (int l = 0; l < 3; l++)
                            {
                                corners[item.X][item.Y][k].Add(0);
                            }
                        }
                        for (int k = 0; k < 4; k++)
                        {
                            for (int l = 0; l < 3; l++)
                            {
                                corners[item.X][item.Y][k][l] = cornerTemp[k][l];
                            }
                        }
                    }
                }
            }
            #endregion
            //sw.Stop();
            //MessageBox.Show(sw.ElapsedMilliseconds.ToString());
            #region algo2
            //List<List<Point>> blocks = new List<List<Point>>();
            //List<List<int>> blnum = new List<List<int>>();
            //List<int> linenum = new List<int>();
            //for (int i = 0; i < sX; i++)
            //{
            //    int now = 0;
            //    blocks.Add(new List<Point> { new Point(i, 0) });
            //    for (int j = 1; j < sY; j++)
            //    {
            //        if (codel[i][j] == codel[i][j - 1]) blocks[now].Add(new Point(i, j));
            //        else
            //        {
            //            blocks.Add(new List<Point> { new Point(i, j) });
            //            ++now;
            //        }
            //        linenum.Add(now);
            //    }
            //}
            #endregion
        }

        void AdvanceDebug()
        {
            //if (waitCount == 0 && !inputRequired)
            //{
            //    currentCodel.X = nextCodel.X;
            //    currentCodel.Y = nextCodel.Y;
            //}
            if (inputRequired)
            {
                inputStr = ReplaceCrLf(tb_input.Text);
                inputRequired = false;
            }
            else
            {
                currentCodel = nextCodel;
                ++stepCount;
            }
            int dX = -(directionPointer - 1) % 2, dY = -(directionPointer - 2) % 2;
            if(codels[currentCodel.X][currentCodel.Y] != PietColors.White)
            {
                int cornerX, cornerY;
                if (directionPointer % 2 == 0)
                {
                    cornerX = corners[currentCodel.X][currentCodel.Y][directionPointer][0];
                    cornerY = corners[currentCodel.X][currentCodel.Y][directionPointer][codelChooser + 1];
                }
                else
                {
                    cornerX = corners[currentCodel.X][currentCodel.Y][directionPointer][codelChooser + 1];
                    cornerY = corners[currentCodel.X][currentCodel.Y][directionPointer][0];
                }
                nextCodel.X = cornerX + dX;
                nextCodel.Y = cornerY + dY;
                if (nextCodel.X < 0 || nextCodel.X > fieldWidth - 1 || nextCodel.Y < 0 || nextCodel.Y > fieldHeight - 1)
                {
                    nextCodel = currentCodel;
                    Wait();
                    return;
                }
                else if (codels[nextCodel.X][nextCodel.Y] == PietColors.Black)
                {
                    nextCodel = currentCodel;
                    Wait();
                    return;
                }
                else if (codels[nextCodel.X][nextCodel.Y] != PietColors.White)
                {
                    waitCount = 0;
                    int nextCodelColor = codels[nextCodel.X][nextCodel.Y], currentCodelColor = codels[currentCodel.X][currentCodel.Y],
                        diff = (nextCodelColor % 3 - currentCodelColor % 3 + 3) % 3 + (nextCodelColor / 3 - currentCodelColor / 3 + 6) % 6 * 3,
                        lastStack = stack.Count - 1;
                    currentCommand = commandNames[diff];
                    switch (diff)
                    {
                        case 1: //push
                            stack.Add(colorBlockSizes[currentCodel.X][currentCodel.Y]);
                            break;
                        case 2: //pop
                            if (stack.Count == 0) break;
                            stack.RemoveAt(lastStack);
                            break;
                        case 3: //add
                            if (stack.Count < 2) break;
                            stack[lastStack - 1] += stack[lastStack];
                            stack.RemoveAt(lastStack);
                            break;
                        case 4: //sub
                            if (stack.Count < 2) break;
                            stack[lastStack - 1] -= stack[lastStack];
                            stack.RemoveAt(lastStack);
                            break;
                        case 5: //multi
                            if (stack.Count < 2) break;
                            stack[lastStack - 1] *= stack[lastStack];
                            stack.RemoveAt(lastStack);
                            break;
                        case 6: //div
                            if (stack.Count < 2) break;
                            if (stack[lastStack] == 0) break;
                            stack[lastStack - 1] /= stack[lastStack];
                            stack.RemoveAt(lastStack);
                            break;
                        case 7: //mod
                            if (stack.Count < 2) break;
                            if (stack[lastStack] == 0) break;
                            stack[lastStack - 1] %= stack[lastStack];
                            if (stack[lastStack] * stack[lastStack - 1] < 0) stack[lastStack - 1] += stack[lastStack];
                            stack.RemoveAt(lastStack);
                            break;
                        case 8: //not
                            if (stack.Count == 0) break;
                            if (stack[lastStack] == 0) stack[lastStack] = 1;
                            else stack[lastStack] = 0;
                            break;
                        case 9: //great
                            if (stack.Count < 2) break;
                            if (stack[lastStack - 1] > stack[lastStack])
                            {
                                stack.RemoveRange(lastStack - 1, 2);
                                stack.Add(1);
                            }
                            else
                            {
                                stack.RemoveRange(lastStack - 1, 2);
                                stack.Add(0);
                            }
                            break;
                        case 10: //point
                            if (stack.Count == 0) break;
                            directionPointer = (int)((directionPointer + stack[lastStack]) % 4 + 4) % 4;
                            stack.RemoveAt(lastStack);
                            break;
                        case 11: //switch
                            if (stack.Count == 0) break;
                            codelChooser = (int)((codelChooser + stack[lastStack]) % 2 + 2) % 2;
                            stack.RemoveAt(lastStack);
                            break;
                        case 12: //dup
                            if (stack.Count == 0) break;
                            stack.Add(stack[lastStack]);
                            break;
                        case 13: //roll
                            if (stack.Count < 2) break;
                            if (stack[lastStack - 1] < 0) break;
                            if (stack.Count - 2 < stack[lastStack - 1]) break;
                            int rd = (int)stack[lastStack - 1], rc = (int)((stack[lastStack] % rd + rd) % rd);
                            for (int i = 0; i < rc; i++)
                            {
                                for (int j = 0; j < rd; j++)
                                {
                                    stack[lastStack - j - 1] = stack[lastStack - j - 2];
                                }
                                stack[lastStack - rd - 1] = stack[lastStack - 1];
                            }
                            stack.RemoveRange(lastStack - 1, 2);
                            break;
                        case 14: //in(n)
                            string[] tmp = inputStr.Split((string[])null, System.StringSplitOptions.RemoveEmptyEntries);
                            if (tmp.Length == 0)
                            {
                                paused = true;
                                inputRequired = true;
                                break;
                            }
                            long innum;
                            if (long.TryParse(tmp[0], out innum))
                            {
                                stack.Add(innum);
                                inputStr = inputStr.Remove(0, inputStr.IndexOf(tmp[0]) + tmp[0].Length);
                            }
                            break;
                        case 15: //in(c)
                            if (inputStr.Length == 0)
                            {
                                paused = true;
                                inputRequired = true;
                                break;
                            }
                            stack.Add(inputStr[0]);
                            inputStr = inputStr.Remove(0, 1);
                            break;
                        case 16: //out(n)
                            if (stack.Count == 0) break;
                            outputStr += stack[lastStack].ToString();
                            stack.RemoveAt(lastStack);
                            break;
                        case 17: //out(c)
                            if (stack.Count == 0) break;
                            try
                            {
                                outputStr += Convert.ToChar(stack[lastStack]);
                            }
                            catch (Exception)
                            {
                                break;
                            }
                            stack.RemoveAt(lastStack);
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    while (true)
                    {
                        nextCodel.X += dX;
                        nextCodel.Y += dY;
                        if (nextCodel.X < 0 || nextCodel.X > fieldWidth - 1 || nextCodel.Y < 0 || nextCodel.Y > fieldHeight - 1)
                        {
                            //Wait();
                            nextCodel.X -= dX;
                            nextCodel.Y -= dY;
                            waitCount = 0;
                            Slide();
                            return;
                        }
                        else if (codels[nextCodel.X][nextCodel.Y] == PietColors.Black)
                        {
                            //Wait();
                            nextCodel.X -= dX;
                            nextCodel.Y -= dY;
                            waitCount = 0;
                            Slide();
                            return;
                        }
                        else if (codels[nextCodel.X][nextCodel.Y] != PietColors.White) //noop
                        {
                            waitCount = slideCount = 0;
                            currentCommand = "noop";
                            return;
                        }
                    }
                }
            }
            else
            {
                while (true)
                {
                    nextCodel.X += dX;
                    nextCodel.Y += dY;
                    if (nextCodel.X < 0 || nextCodel.X > fieldWidth - 1 || nextCodel.Y < 0 || nextCodel.Y > fieldHeight - 1)
                    {
                        //Wait();
                        nextCodel.X -= dX;
                        nextCodel.Y -= dY;
                        Slide();
                        return;
                    }
                    else if (codels[nextCodel.X][nextCodel.Y] == PietColors.Black)
                    {
                        //Wait();
                        nextCodel.X -= dX;
                        nextCodel.Y -= dY;
                        Slide();
                        return;
                    }
                    else if (codels[nextCodel.X][nextCodel.Y] != PietColors.White) //noop
                    {
                        waitCount = slideCount = 0;
                        currentCommand = "noop";
                        return;
                    }
                }
            }
        }

        void Wait()
        {
            if (waitCount == 7)
            {
                EndDebug("デバッグが終了しました。");
                return;
            }
            if (waitCount % 2 == 0) codelChooser = (codelChooser + 1) % 2;
            else directionPointer = (directionPointer + 1) % 4;
            ++waitCount;
            currentCommand = "wait(" + waitCount.ToString() + ")";
        }

        void Slide()
        {
            if (slideCount == 0) {
                slideCodels.Clear();
                slideCodels.Add(nextCodel);
            }
            else if(slideCodels[slideCodels.Count - 1] != nextCodel)
            {
                slideCodels.Add(nextCodel);
                for (int i = 0, n = slideCodels.Count - 2; i < n; i++)
                {
                    if(slideCodels[i] == nextCodel)
                    {
                        EndDebug("デバッグが終了しました。");
                        return;
                    }
                }
            }
            if (slideCount % 2 == 0) codelChooser = (codelChooser + 1) % 2;
            else directionPointer = (directionPointer + 1) % 4;
            ++slideCount;
            currentCommand = "slide(" + slideCount.ToString() + ")";
        }

        void StartDebug(bool stepF = false, bool jumpF = false)
        {
            if (fieldWidth == 1 && fieldHeight == 1)
            {
                MessageBox.Show("codelが1個しかありません。。");
                return;
            }
            if (codels[0][0] == PietColors.Black)
            {
                MessageBox.Show("左上のcodelが黒です。");
                return;
            }
            int jumpT = 0;
            if (jumpF)
            {
                jumpT = InputBoxes.NumericInputBox("ジャンプする回数を指定して下さい。", "Pidet", 1, 1, 1000000000);
                if (jumpT == 0) return;
            }
            if (editMode != Mode.DEBUG_MODE) PrepareDebug();
            if (stepF)
            {
                AdvanceDebug();
            }
            else if (jumpF)
            {
                int stepCount = 0;
                paused = false;
                while (!paused)
                {
                    AdvanceDebug();
                    ++stepCount;
                    if (nextCodel.X > -1 && nextCodel.X < fieldWidth && nextCodel.Y > -1 && nextCodel.Y < fieldHeight)
                    {
                        if (breakPoints[nextCodel.X][nextCodel.Y])
                        {
                            paused = true;
                            break;
                        }
                    }
                    if (stepCount == jumpT)
                    {
                        paused = true;
                    }
                }
            }
            else
            {
                paused = false;
                int stepCount = 0;
                while (!paused)
                {
                    AdvanceDebug();
                    ++stepCount;
                    if (nextCodel.X > -1 && nextCodel.X < fieldWidth && nextCodel.Y > -1 && nextCodel.Y < fieldHeight)
                    {
                        if (breakPoints[nextCodel.X][nextCodel.Y])
                        {
                            paused = true;
                            break;
                        }
                    }
                    if (stepCount == 1000000)
                    {
                        DialogResult result = MessageBox.Show("処理が1,000,000回続いています。中断しますか？", "Pidet", MessageBoxButtons.YesNo);
                        if (result == DialogResult.Yes) EndDebug("デバッグを中断しました。");
                        else stepCount = 0;
                    }
                }
            }
            if (editMode == Mode.DEBUG_MODE) PauseDebug();
        }

        void PauseDebug()
        {
            if (inputRequired)
            {
                MessageBox.Show("インプットが必要です。");
            }
            RefreshDebugStatus();
        }

        void RefreshDebugStatus()
        {
            tb_input.Text = ReplaceCrLf(inputStr, false);
            tb_output.Text = ReplaceCrLf(outputStr, false);
            string stackStr = "";
            foreach (long item in stack)
            {
                if (item < 32) stackStr += item.ToString() + "(??)\r\n";
                else
                {
                    try
                    {
                        stackStr += item.ToString() + "(" + Convert.ToChar(item) + ")\r\n";
                    }
                    catch (Exception)
                    {
                        stackStr += item.ToString() + "(??)\r\n";
                    }
                }
            }
            tb_stackbefore.Text = tb_stack.Text;
            tb_stack.Text = stackStr;
            dgv_field.Refresh();
        }

        void EndDebug(string msg = "")
        {
            if (msg != "") MessageBox.Show(msg);
            paused = true;
            EndDebugMode();
            tb_input.Text = ReplaceCrLf(inputStrTmp, false);
            tb_output.Text = ReplaceCrLf(outputStr, false);
            tb_stack.Text = "";
            tb_stackbefore.Text = "";
            dgv_field.Refresh();
        }

        void ResetDebug()
        {
            EndDebug("デバッグを中断しました。");
        }

        #endregion

        #region Edit

        void SetSaveRequired(bool e)
        {
            saveRequired = e;
            if (historyIndex > 0 && saveRequired) this.Text = fileName + " * - Pidet";
            else this.Text = fileName + " - Pidet";
        }

        void AddHistory()
        {
            ++historyIndex;
            SetSaveRequired(true);
            int c = history.Count;
            for (int i = historyIndex; i < c; i++)
            {
                history.RemoveAt(historyIndex);
            }
            history.Add(new List<List<int>>());
            for (int i = 0; i < fieldWidth; i++)
            {
                history[historyIndex].Add(new List<int>());
                for (int j = 0; j < fieldHeight; j++)
                {
                    history[historyIndex][i].Add(codels[i][j]);
                }
            }
        }

        void UndoHistory()
        {
            if (historyIndex < 1) return;
            --historyIndex;
            SynchronizeHistoryToCodel();
        }

        void RedoHistory()
        {
            if (historyIndex == history.Count - 1) return;
            ++historyIndex;
            SynchronizeHistoryToCodel();
        }

        void ResetHistory()
        {
            history.Clear();
            historyIndex = -1;
            AddHistory();
        }

        void SynchronizeHistoryToCodel()
        {
            //codel.Clear();
            //sX = history[hisIndex].Count;
            //sY = history[hisIndex][0].Count;
            //for (int i = 0; i < sX; i++)
            //{
            //    codel.Add(new List<int>());
            //    for (int j = 0; j < sY; j++)
            //    {
            //        codel[i].Add(history[hisIndex][i][j]);
            //    }
            //}
            //RefreshField();
            ChangeSX(history[historyIndex].Count);
            ChangeSY(history[historyIndex][0].Count);
            for (int i = 0; i < fieldWidth; i++)
            {
                for (int j = 0; j < fieldHeight; j++)
                {
                    codels[i][j] = history[historyIndex][i][j];
                }
            }
            RefreshField();
        }

        void RefreshField()
        {
            ChangeFieldX(fieldWidth);
            ChangeFieldY(fieldHeight);
            for (int i = 0; i < fieldWidth; i++)
            {
                for (int j = 0; j < fieldHeight; j++)
                {
                    dgv_field[i, j].Style.BackColor = ColorByIndex(codels[i][j]);
                }
            }
        }

        void ResetCodel()
        {
            codels.Clear();
            for (int i = 0; i < fieldWidth; i++)
            {
                codels.Add(new List<int>());
                for (int j = 0; j < fieldHeight; j++)
                {
                    codels[i].Add(PietColors.White);
                }
            }
        }

        void ResetBP()
        {
            breakPoints.Clear();
            for (int i = 0; i < fieldWidth; i++)
            {
                breakPoints.Add(new List<bool>());
                for (int j = 0; j < fieldHeight; j++)
                {
                    breakPoints[i].Add(false);
                }
            }
        }

        void ChangeFieldX(int e)
        {
            if (e < 1) return;
            int fieldX = dgv_field.ColumnCount;
            if (e < fieldX)
            {
                for (int i = e; i < fieldX; i++)
                {
                    dgv_field.Columns.RemoveAt(e);
                }
            }
            else if (e > fieldX)
            {
                for (int i = fieldX; i < e; i++)
                {
                    dgv_field.Columns.Add("", "");
                    dgv_field.Columns[i].Width = codelSize;
                }
            }
        }

        void ChangeFieldY(int e)
        {
            if (e < 1) return;
            int fieldY = dgv_field.RowCount;
            if (e < fieldY)
            {
                for (int i = e; i < fieldY; i++)
                {
                    dgv_field.Rows.RemoveAt(e);
                }
            }
            else if (e > fieldY)
            {
                for (int i = fieldY; i < e; i++)
                {
                    dgv_field.Rows.Add();
                    dgv_field.Rows[i].Height = codelSize;
                }
            }
        }

        void ChangeCSize(int e)
        {
            if (e < 5) return;
            codelSize = e;
            for (int i = 0; i < fieldWidth; i++)
            {
                dgv_field.Columns[i].Width = codelSize;
            }
            for (int i = 0; i < fieldHeight; i++)
            {
                dgv_field.Rows[i].Height = codelSize;
            }
        }

        void ChangeSX(int e)
        {
            if (e < 1) return;
            if (e < fieldWidth)
            {
                for (int i = e; i < fieldWidth; i++)
                {
                    codels.RemoveAt(e);
                    breakPoints.RemoveAt(e);
                    dgv_field.Columns.RemoveAt(e);
                }
            }
            else if (e > fieldWidth)
            {
                for (int i = fieldWidth; i < e; i++)
                {
                    codels.Add(new List<int>());
                    breakPoints.Add(new List<bool>());
                    for (int j = 0; j < fieldHeight; j++)
                    {
                        codels[i].Add(PietColors.White);
                        breakPoints[i].Add(false);
                    }
                    dgv_field.Columns.Add("", "");
                    dgv_field.Columns[i].Width = codelSize;
                }
            }
            fieldWidth = e;
        }

        void ChangeSY(int e)
        {
            if (e < 1) return;
            if (e < fieldHeight)
            {
                for (int i = e; i < fieldHeight; i++)
                {
                    for (int j = 0; j < fieldWidth; j++)
                    {
                        codels[j].RemoveAt(e);
                        breakPoints[j].RemoveAt(e);
                    }
                    dgv_field.Rows.RemoveAt(e);
                }
            }
            else if (e > fieldHeight)
            {
                for (int i = fieldHeight; i < e; i++)
                {
                    for (int j = 0; j < fieldWidth; j++)
                    {
                        codels[j].Add(PietColors.White);
                        breakPoints[j].Add(false);
                    }
                    dgv_field.Rows.Add();
                    dgv_field.Rows[i].Height = codelSize;
                }
            }
            fieldHeight = e;
        }

        void ChangeSXSY()
        {
            int newSX = InputBoxes.NumericInputBox("キャンバスの幅を指定して下さい。", "Pidet", fieldWidth, 1, 100000);
            if (newSX == 0) return;
            int newSY = InputBoxes.NumericInputBox("キャンバスの高さを指定して下さい。", "Pidet", fieldHeight, 1, 100000);
            if (newSY == 0) return;
            ChangeSX(newSX);
            ChangeSY(newSY);
            AddHistory();
        }

        Color ColorByIndex(int e)
        {
            const int C0 = 192, FF = 255;
            switch (e)
            {
                case PietColors.LRed:
                    return Color.FromArgb(FF, C0, C0);
                case PietColors.Red:
                    return Color.FromArgb(FF, 0, 0);
                case PietColors.DRed:
                    return Color.FromArgb(C0, 0, 0);
                case PietColors.LYellow:
                    return Color.FromArgb(FF, FF, C0);
                case PietColors.Yellow:
                    return Color.FromArgb(FF, FF, 0);
                case PietColors.DYellow:
                    return Color.FromArgb(C0, C0, 0);
                case PietColors.LGreen:
                    return Color.FromArgb(C0, FF, C0);
                case PietColors.Green:
                    return Color.FromArgb(0, FF, 0);
                case PietColors.DGreen:
                    return Color.FromArgb(0, C0, 0);
                case PietColors.LCyan:
                    return Color.FromArgb(C0, FF, FF);
                case PietColors.Cyan:
                    return Color.FromArgb(0, FF, FF);
                case PietColors.DCyan:
                    return Color.FromArgb(0, C0, C0);
                case PietColors.LBlue:
                    return Color.FromArgb(C0, C0, FF);
                case PietColors.Blue:
                    return Color.FromArgb(0, 0, FF);
                case PietColors.DBlue:
                    return Color.FromArgb(0, 0, C0);
                case PietColors.LMagenta:
                    return Color.FromArgb(FF, C0, FF);
                case PietColors.Magenta:
                    return Color.FromArgb(FF, 0, FF);
                case PietColors.DMagenta:
                    return Color.FromArgb(C0, 0, C0);
                case PietColors.White:
                    return Color.FromArgb(FF, FF, FF);
                case PietColors.Black:
                    return Color.FromArgb(0, 0, 0);
                default:
                    return Color.FromArgb(FF, FF, FF);
            }
        }

        Color ColorByIndexEx(int e, int dark = 0, int middle = 192, int light = 255)
        {
            switch (e)
            {
                case PietColors.LRed:
                    return Color.FromArgb(light, middle, middle);
                case PietColors.Red:
                    return Color.FromArgb(light, dark, dark);
                case PietColors.DRed:
                    return Color.FromArgb(middle, dark, dark);
                case PietColors.LYellow:
                    return Color.FromArgb(light, light, middle);
                case PietColors.Yellow:
                    return Color.FromArgb(light, light, dark);
                case PietColors.DYellow:
                    return Color.FromArgb(middle, middle, dark);
                case PietColors.LGreen:
                    return Color.FromArgb(middle, light, middle);
                case PietColors.Green:
                    return Color.FromArgb(dark, light, dark);
                case PietColors.DGreen:
                    return Color.FromArgb(dark, middle, dark);
                case PietColors.LCyan:
                    return Color.FromArgb(middle, light, light);
                case PietColors.Cyan:
                    return Color.FromArgb(dark, light, light);
                case PietColors.DCyan:
                    return Color.FromArgb(dark, middle, middle);
                case PietColors.LBlue:
                    return Color.FromArgb(middle, middle, light);
                case PietColors.Blue:
                    return Color.FromArgb(dark, dark, light);
                case PietColors.DBlue:
                    return Color.FromArgb(dark, dark, middle);
                case PietColors.LMagenta:
                    return Color.FromArgb(light, middle, light);
                case PietColors.Magenta:
                    return Color.FromArgb(light, dark, light);
                case PietColors.DMagenta:
                    return Color.FromArgb(middle, dark, middle);
                case PietColors.White:
                    return Color.FromArgb(light, light, light);
                case PietColors.Black:
                    return Color.FromArgb(dark, dark, dark);
                default:
                    return Color.FromArgb(light, light, light);
            }
        }

        int IndexByColor(Color e)
        {
            for (int i = 0; i < 20; i++)
            {
                if (e.Equals(ColorByIndex(i))) return i;
            }
            return PietColors.White;
        }

        void ChangeColor(int cX,int cY,int e)
        {
            codels[cX][cY] = e;
            dgv_field[cX, cY].Style.BackColor = ColorByIndex(e);
        }

        void ChangeBP(int cX, int cY, bool e)
        {
            breakPoints[cX][cY] = e;
            dgv_field.Refresh();
        }

        void ChangeCurrentColor(int e)
        {
            if (e < 0 || e > 19) return;
            currentColor = e;
            dgv_palette[2, 6].Style.BackColor = ColorByIndex(e);
            dgv_palette[2, 6].Style.SelectionBackColor = ColorByIndex(e);
        }

        void ChangeStandardColor(int e)
        {
            if (e < 0 || e > 17) return;
            standardColor = e;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    dgv_palette[(i + e % 3) % 3, (j + e / 3) % 6].Value = commandNames[i + j * 3];
                }
            }
        }

        void SelectColorBlock(int cX, int cY)
        {
            dgv_field.ClearSelection();
            //TrySelectColorBlock(cX, cY, codel[cX][cY]);
            List<Point> e = new List<Point> { new Point(cX, cY) };
            while (e.Count > 0)
            {
                int eX = e[0].X, eY = e[0].Y;
                if ((eX > -1 && eX < fieldWidth && eY > -1 && eY < fieldHeight) && (!dgv_field[eX, eY].Selected && codels[eX][eY] == codels[cX][cY]))
                {
                    dgv_field[eX, eY].Selected = true;
                    e.Add(new Point(eX - 1, eY));
                    e.Add(new Point(eX + 1, eY));
                    e.Add(new Point(eX, eY - 1));
                    e.Add(new Point(eX, eY + 1));
                }
                e.RemoveAt(0);
            }
            dgv_field.CurrentCell = dgv_field[cX, cY];
        }

        void TrySelectColorBlock(int cX, int cY, int e)
        {
            if (cX < 0 || cX > fieldWidth - 1 || cY < 0 || cY > fieldHeight - 1) return;
            if (dgv_field[cX, cY].Selected || codels[cX][cY] != e) return;
            dgv_field[cX, cY].Selected = true;
            TrySelectColorBlock(cX - 1, cY, e);
            TrySelectColorBlock(cX + 1, cY, e);
            TrySelectColorBlock(cX, cY - 1, e);
            TrySelectColorBlock(cX, cY + 1, e);
        }

        void RotateColor(int e)
        {
            int curX = dgv_field.CurrentCellAddress.X, curY = dgv_field.CurrentCellAddress.Y;
            if (codels[curX][curY] == PietColors.Black || codels[curX][curY] == PietColors.White) return;
            int dX = (e % 3 - codels[curX][curY] % 3 + 3) % 3, dY = (e / 3 - codels[curX][curY] / 3 + 6) % 6;
            foreach (DataGridViewCell item in dgv_field.SelectedCells)
            {
                int oldColor = codels[item.ColumnIndex][item.RowIndex];
                if (oldColor == PietColors.White || oldColor == PietColors.Black) continue;
                ChangeColor(item.ColumnIndex, item.RowIndex, (oldColor % 3 + dX) % 3 + (oldColor / 3 + dY) % 6 * 3);
            }
            AddHistory();
        }

        bool CopyRect()
        {
            int l, r, t, b, cnt = 0;
            l = r = dgv_field.CurrentCellAddress.X;
            t = b = dgv_field.CurrentCellAddress.Y;
            foreach (DataGridViewCell item in dgv_field.SelectedCells)
            {
                if (item.ColumnIndex < l) l = item.ColumnIndex;
                else if (item.ColumnIndex > r) r = item.ColumnIndex;
                if (item.RowIndex < t) t = item.RowIndex;
                else if (item.RowIndex > b) b = item.RowIndex;
                ++cnt;
            }
            if ((r - l + 1) * (b - t + 1) != cnt)
            {
                MessageBox.Show("矩形選択をして下さい。");
                return false;
            }
            buffer.Clear();
            for (int i = 0; i < r - l + 1; i++)
            {
                buffer.Add(new List<int>());
                for (int j = 0; j < b - t + 1; j++)
                {
                    buffer[i].Add(codels[l + i][t + j]);
                }
            }
            return true;
        }

        void CutRect()
        {
            if (!CopyRect()) return;
            foreach (DataGridViewCell item in dgv_field.SelectedCells)
            {
                ChangeColor(item.ColumnIndex, item.RowIndex, PietColors.White);
            }
            AddHistory();
        }

        void PasteRect()
        {
            if (buffer.Count == 0) return;
            int l, t;
            l = dgv_field.CurrentCellAddress.X;
            t = dgv_field.CurrentCellAddress.Y;
            foreach (DataGridViewCell item in dgv_field.SelectedCells)
            {
                if (item.ColumnIndex < l) l = item.ColumnIndex;
                if (item.RowIndex < t) t = item.RowIndex;
            }
            int icnt = Math.Min(fieldWidth - l, buffer.Count), jcnt = Math.Min(fieldHeight - t, buffer[0].Count);
            for (int i = 0; i < icnt; i++)
            {
                for (int j = 0; j < jcnt; j++)
                {
                    ChangeColor(l + i, t + j, buffer[i][j]);
                }
            }
            AddHistory();
        }

        void Translate(int dX, int dY)
        {
            List<List<int>> temp = new List<List<int>>();
            for (int i = 0; i < fieldWidth; i++)
            {
                temp.Add(new List<int>());
                for (int j = 0; j < fieldHeight; j++)
                {
                    if (i < dX || i > fieldWidth + dX - 1 || j < dY || j > fieldHeight + dY - 1)
                    {
                        temp[i].Add(PietColors.White);
                    }else
                    {
                        temp[i].Add(codels[i - dX][j - dY]);
                    }
                }
            }
            for (int i = 0; i < fieldWidth; i++)
            {
                for (int j = 0; j < fieldHeight; j++)
                {
                    ChangeColor(i, j, temp[i][j]);
                }
            }
            AddHistory();
        }

        #endregion

        #region File

        void CreateNew()
        {
            if (historyIndex > 0 && saveRequired)
            {
                DialogResult result = MessageBox.Show(fileName + "への変更内容を保存しますか？", "Pidet", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation);
                if (result == DialogResult.Yes)
                {
                    SaveFile();
                }
                else if (result == DialogResult.Cancel)
                {
                    return;
                }
            }
            int newSX = InputBoxes.NumericInputBox("キャンバスの幅を指定して下さい。", "Pidet", 10, 1, 100000);
            if (newSX == 0) return;
            int newSY = InputBoxes.NumericInputBox("キャンバスの高さを指定して下さい。", "Pidet", 10, 1, 100000);
            if (newSY == 0) return;
            if (editMode == Mode.DEBUG_MODE) EndDebug();
            fieldWidth = newSX;
            fieldHeight = newSY;
            fileCodelSize = 10;
            fileName = "NoName";
            //this.Text = fileName + " - Pidet";
            filePath = "";
            ResetCodel();
            ResetBP();
            RefreshField();
            ResetHistory();
            SetSaveRequired(false);
        }

        void OpenFile(string openFilePath = "")
        {
            if (historyIndex > 0 && saveRequired)
            {
                DialogResult result = MessageBox.Show(fileName + "への変更内容を保存しますか？", "Pidet", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation);
                if (result == DialogResult.Yes)
                {
                    SaveFile();
                }
                else if (result == DialogResult.Cancel)
                {
                    return;
                }
            }
            if (openFilePath == "")
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = FILE_FORMAT_FILTER;
                if (ofd.ShowDialog() != DialogResult.OK) return;
                openFilePath = ofd.FileName;
            }
            int openCSize = InputBoxes.NumericInputBox("コーデルサイズを指定して下さい。", "Pidet", 10, 1, 10000);
            if (openCSize < 1) return;
            if (editMode == Mode.DEBUG_MODE) EndDebug();
            FileStream fs = null;
            Bitmap bmp = null;
            BitmapFrame bmpframe = null;
            try
            {
                fs = new FileStream(openFilePath, FileMode.Open, FileAccess.Read);
                bmp = (Bitmap)Bitmap.FromStream(fs);
                bmpframe = BitmapFrame.Create(fs);
            }
            catch (Exception)
            {
                MessageBox.Show("ファイルの読み込みに失敗しました。");
                return;
            }
            finally
            {
                fs.Close();
            }
            int oX = bmp.Width / openCSize, oY = bmp.Height / openCSize;
            if (oX == 0 || oY == 0 || bmp.Width % openCSize != 0 || bmp.Height % openCSize != 0)
            {
                MessageBox.Show("画像サイズが不適切です。");
                bmp.Dispose();
                return;
            }
            fileCodelSize = openCSize;
            fileName = Path.GetFileName(openFilePath);
            //this.Text = fileName + " - Pidet";
            filePath = openFilePath;
            fieldWidth = oX;
            fieldHeight = oY;
            codels.Clear();
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            byte[] buf = new Byte[bmp.Width * bmp.Height * 4];
            Marshal.Copy(bmpData.Scan0, buf, 0, buf.Length);
            int stride = bmpData.Stride;
            for (int i = 0; i < fieldWidth; i++)
            {
                codels.Add(new List<int>());
                for (int j = 0; j < fieldHeight; j++)
                {
                    //codel[i].Add(IndexByColor(bmp.GetPixel(i * openCSize, j * openCSize)));
                    int pos = (i * 4 + j * stride) * openCSize;
                    codels[i].Add(IndexByColor(Color.FromArgb(buf[pos + 2], buf[pos + 1], buf[pos])));
                }
            }
            Marshal.Copy(buf, 0, bmpData.Scan0, buf.Length);
            bmp.UnlockBits(bmpData);
            bmp.Dispose();

            BitmapMetadata metadata = (BitmapMetadata)bmpframe.Metadata.Clone();
            for (int i = 0;  (string)metadata.GetQuery("/[" + i.ToString() + "]iTXt/Keyword") != null; ++i)
            {
                string iTXtKeyword = (string)metadata.GetQuery("/[" + i.ToString() + "]iTXt/Keyword");
                string iTXtTextEntry = (string)metadata.GetQuery("/[" + i.ToString() + "]iTXt/TextEntry");
                if (iTXtKeyword == "Piet Source Code Comment(CAPPNG)")
                {
                    comments.Add(Comment.Decode(iTXtTextEntry));
                }
            }

            //sw.Stop();
            //MessageBox.Show(sw.ElapsedMilliseconds.ToString());
            ResetBP();
            RefreshField();
            ResetHistory();
            SetSaveRequired(false);
        }

        void SaveFile()
        {
            if (filePath == "")
            {
                SaveFileAs();
                return;
            }
            Bitmap bmp = new Bitmap(fieldWidth * fileCodelSize, fieldHeight * fileCodelSize);
            Graphics g = Graphics.FromImage(bmp);
            SolidBrush[] b=new SolidBrush[20];
            for (int i = 0; i < 20; i++)
			{
                b[i] = new SolidBrush(ColorByIndex(i));
			}
            for (int i = 0; i < fieldWidth; i++)
            {
                for (int j = 0; j < fieldHeight; j++)
                {
                    g.FillRectangle(b[codels[i][j]], i * fileCodelSize, j * fileCodelSize, fileCodelSize, fileCodelSize);
                }
            }
            for (int i = 0; i < 20; i++)
            {
                b[i].Dispose();
            }
            g.Dispose();
            try
            {
                bmp.Save(filePath,FormatByName(filePath));
                SetSaveRequired(false);
            }
            catch (Exception)
            {
                MessageBox.Show("保存に失敗しました。");
            }
        }

        void SaveFileAs()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = fileName;
            sfd.Filter = FILE_FORMAT_FILTER;
            if (sfd.ShowDialog() != DialogResult.OK) return;
            int saveCSize = InputBoxes.NumericInputBox("コーデルサイズを指定して下さい。", "Pidet", fileCodelSize, 1, 10000);
            if (saveCSize == 0) return;
            fileCodelSize = saveCSize;
            fileName = Path.GetFileName(sfd.FileName);
            //this.Text = fileName + " - Pidet";
            filePath = sfd.FileName;
            Bitmap bmp = new Bitmap(fieldWidth * fileCodelSize, fieldHeight * fileCodelSize);
            Graphics g = Graphics.FromImage(bmp);
            SolidBrush[] b = new SolidBrush[20];
            for (int i = 0; i < 20; i++)
            {
                b[i] = new SolidBrush(ColorByIndex(i));
            }
            for (int i = 0; i < fieldWidth; i++)
            {
                for (int j = 0; j < fieldHeight; j++)
                {
                    g.FillRectangle(b[codels[i][j]], i * fileCodelSize, j * fileCodelSize, fileCodelSize, fileCodelSize);
                }
            }
            for (int i = 0; i < 20; i++)
            {
                b[i].Dispose();
            }
            g.Dispose();
            try
            {
                bmp.Save(filePath, FormatByName(filePath));
                SetSaveRequired(false);
            }
            catch (Exception)
            {
                MessageBox.Show("保存に失敗しました。");
            }
        }

        void SaveFileAsEx()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = fileName;
            sfd.Filter = "Image|*.bmp;*.gif;*.png;*.jpg|Bitmap|*.bmp|GIF|*.gif|JPEG|*.jpg|PNG|*.png";
            sfd.Title = "Piet - RGB SelectMode";
            if (sfd.ShowDialog() != DialogResult.OK) return;
            int saveCSize = InputBoxes.NumericInputBox("コーデルサイズを指定して下さい。", "Pidet", fileCodelSize, 1, 10000);
            if (saveCSize == 0) return;
            fileCodelSize = saveCSize;
            fileName = Path.GetFileName(sfd.FileName);
            //this.Text = fileName + " - Pidet";
            filePath = sfd.FileName;
            Bitmap bmp = new Bitmap(fieldWidth * fileCodelSize, fieldHeight * fileCodelSize);
            Graphics g = Graphics.FromImage(bmp);
            SolidBrush[] b = new SolidBrush[20];
            for (int i = 0; i < 20; i++)
            {
                b[i] = new SolidBrush(ColorByIndexEx(i,0,127,255));
            }
            for (int i = 0; i < fieldWidth; i++)
            {
                for (int j = 0; j < fieldHeight; j++)
                {
                    g.FillRectangle(b[codels[i][j]], i * fileCodelSize, j * fileCodelSize, fileCodelSize, fileCodelSize);
                }
            }
            for (int i = 0; i < 20; i++)
            {
                b[i].Dispose();
            }
            g.Dispose();
            try
            {
                bmp.Save(filePath);
                SetSaveRequired(false);
            }
            catch (Exception)
            {
                MessageBox.Show("保存に失敗しました。");
            }
        }

        ImageFormat FormatByName(string fileName)
        {
            ImageFormat ret = ImageFormat.Bmp;
            string ext = Path.GetExtension(fileName).ToLower();
            switch (ext)
            {
                case ".png":
                    ret = ImageFormat.Png;
                    break;
                default:
                    break;
            }
            return ret;
        }

        #endregion

        public Main()
        {
            InitializeComponent();

            typeof(DataGridView).GetProperty
                ("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(dgv_field, true, null);
            typeof(DataGridView).GetProperty
                ("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(dgv_palette, true, null);
            this.dgv_field.MouseWheel+=dgv_field_MouseWheel;

            this.Text = fileName + " - Pidet";

            for (int i = 0; i < 3; i++)
            {
                dgv_palette.Columns.Add("", "");
                dgv_palette.Columns[i].Width = 50;
            }
            for (int i = 0; i < 7; i++)
            {
                dgv_palette.Rows.Add();
                dgv_palette.Rows[i].Height = 50;
            }
            dgv_palette.Width = 51 * 3;
            dgv_palette.Height = 51 * 7 - 4;
            for (int i = 0; i < 20; i++)
            {
                dgv_palette[i % 3, i / 3].Style.BackColor = ColorByIndex(i);
                dgv_palette[i % 3, i / 3].Style.SelectionBackColor = ColorByIndex(i);
                int e = (i % 3 + 2) * 60;
                dgv_palette[i % 3, i / 3].Style.ForeColor = Color.FromArgb(e, e, e);
                dgv_palette[i % 3, i / 3].Style.SelectionForeColor = Color.FromArgb(e, e, e);
            }
            ChangeCurrentColor(0);
            ChangeStandardColor(0);

            ResetCodel();
            ResetBP();
            RefreshField();
        }

        private void main_Load(object sender, EventArgs e)
        {
            ChangeEditMode(0);
            AddHistory();
            tm_status.Enabled = true;
            dgv_field.Select();
            string[] cmds = System.Environment.GetCommandLineArgs();
            if (cmds.Length > 1) OpenFile(cmds[1]);
        }

        private void main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (historyIndex > 0 && saveRequired)
            {
                DialogResult result = MessageBox.Show(fileName + "への変更内容を保存しますか？", "Pidet", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation);
                if (result == DialogResult.Yes)
                {
                    SaveFile();
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        private void dgv_field_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (editMode == Mode.DEBUG_MODE)
            {
                if (e.ColumnIndex == currentCodel.X && e.RowIndex == currentCodel.Y)
                {
                    e.Paint(e.CellBounds, e.PaintParts & ~DataGridViewPaintParts.Focus & ~DataGridViewPaintParts.SelectionBackground);
                    ControlPaint.DrawGrid(e.Graphics, e.CellBounds, new Size(3, 3), e.CellStyle.BackColor);
                }
                else if (e.ColumnIndex == nextCodel.X && e.RowIndex == nextCodel.Y)
                {
                    e.Paint(e.CellBounds, e.PaintParts & ~DataGridViewPaintParts.Focus & ~DataGridViewPaintParts.SelectionBackground);
                    ControlPaint.DrawGrid(e.Graphics, e.CellBounds, new Size(4, 4), e.CellStyle.BackColor);
                }
                else
                {
                    e.Paint(e.CellBounds, e.PaintParts & ~DataGridViewPaintParts.Focus & ~DataGridViewPaintParts.SelectionBackground);
                }
            }
            else
            {
                if (((DataGridView)sender).CurrentCellAddress.X == e.ColumnIndex && ((DataGridView)sender).CurrentCellAddress.Y == e.RowIndex)
                {
                    e.Paint(e.CellBounds, e.PaintParts & ~DataGridViewPaintParts.Focus & ~DataGridViewPaintParts.SelectionBackground);
                    ControlPaint.DrawGrid(e.Graphics, e.CellBounds, new Size(3, 3), e.CellStyle.BackColor);
                }
                else if (dgv_field[e.ColumnIndex, e.RowIndex].Selected)
                {
                    e.Paint(e.CellBounds, e.PaintParts & ~DataGridViewPaintParts.Focus & ~DataGridViewPaintParts.SelectionBackground);
                    //ControlPaint.DrawFocusRectangle(e.Graphics, e.CellBounds);
                    ControlPaint.DrawGrid(e.Graphics, e.CellBounds, new Size(4, 4), e.CellStyle.BackColor);
                }
                else
                {
                    e.Paint(e.CellBounds, e.PaintParts & ~DataGridViewPaintParts.Focus & ~DataGridViewPaintParts.SelectionBackground);
                }
            }
            if (breakPoints[e.ColumnIndex][e.RowIndex])
            {
                Point[] points = 
                    { new Point(e.CellBounds.X, e.CellBounds.Y), 
                        new Point(e.CellBounds.X+ codelSize / 2, e.CellBounds.Y), new Point(e.CellBounds.X,e.CellBounds.Y+ codelSize / 2) };
                e.Graphics.FillPolygon(Brushes.Red, points);
                e.Graphics.DrawLine(Pens.White, e.CellBounds.X + codelSize / 2, e.CellBounds.Y, e.CellBounds.X, e.CellBounds.Y + codelSize / 2);
                e.Graphics.DrawLine(Pens.White, e.CellBounds.X + codelSize / 3, e.CellBounds.Y, e.CellBounds.X, e.CellBounds.Y + codelSize / 3);
            }
            e.Handled = true;
        }

        private void dgv_field_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) ChangeCurrentColor(codels[e.ColumnIndex][e.RowIndex]);
        }

        private void main_KeyDown(object sender, KeyEventArgs e)
        {
            //if (e.KeyData == Keys.D)
            //    MessageBox.Show(dgv_field[0, 0].Style.BackColor.A.ToString());
            if (e.KeyData == (Keys.Control | Keys.N)) CreateNew();
            if (e.KeyData == (Keys.Control | Keys.O)) OpenFile();
            if (e.KeyData == (Keys.Control | Keys.S)) SaveFile();
            if (e.KeyData == (Keys.Control | Keys.Shift | Keys.S)) SaveFileAs();
            //if (e.KeyData == (Keys.Control | Keys.Alt | Keys.Shift | Keys.S)) SaveFileAsEx();

            if (editMode == Mode.DEBUG_MODE)
            {
                if (e.KeyData == Keys.Escape || (e.KeyData == (Keys.Shift | Keys.F5))) ResetDebug();
            }
            if (e.KeyData == Keys.F5) { StartDebug(); e.Handled = true; }
            if (e.KeyData == Keys.F10) { StartDebug(false, true); e.Handled = true; }
            if (e.KeyData == Keys.F11) { StartDebug(true); e.Handled = true; }

            if (editMode != Mode.DEBUG_MODE)
            {
                if (e.KeyData == (Keys.Control | Keys.K)) ChangeCurrentColor(codels[dgv_field.CurrentCellAddress.X][dgv_field.CurrentCellAddress.Y]);
                if (e.KeyData == (Keys.Control | Keys.R)) ChangeSXSY();
                if (e.KeyData == (Keys.Control | Keys.Alt | Keys.Left)) { ChangeSX(fieldWidth - 1); AddHistory(); e.Handled = true; }
                if (e.KeyData == (Keys.Control | Keys.Alt | Keys.Right)) { ChangeSX(fieldWidth + 1); AddHistory(); e.Handled = true; }
                if (e.KeyData == (Keys.Control | Keys.Alt | Keys.Up)) { ChangeSY(fieldHeight - 1); AddHistory(); e.Handled = true; }
                if (e.KeyData == (Keys.Control | Keys.Alt | Keys.Down)) { ChangeSY(fieldHeight + 1); AddHistory(); e.Handled = true; }
                if (e.KeyData == (Keys.Shift | Keys.Alt | Keys.Left)) { Translate(-1, 0); e.Handled = true; }
                if (e.KeyData == (Keys.Shift | Keys.Alt | Keys.Right)) { Translate(1, 0); e.Handled = true; }
                if (e.KeyData == (Keys.Shift | Keys.Alt | Keys.Up)) { Translate(0, -1); e.Handled = true; }
                if (e.KeyData == (Keys.Shift | Keys.Alt | Keys.Down)) { Translate(0, 1); e.Handled = true; }
                if (e.KeyData == (Keys.Control | Keys.Enter) || e.KeyData == (Keys.Control | Keys.Space))
                { ChangeCurrentColor(codels[dgv_field.CurrentCellAddress.X][dgv_field.CurrentCellAddress.Y]); e.Handled = true; }
            }

            if (e.KeyData == (Keys.Control | Keys.Oemplus)) ChangeCSize(codelSize + 1);
            if (e.KeyData == (Keys.Control | Keys.OemMinus)) ChangeCSize(codelSize - 1);
            if (e.KeyData == (Keys.Control | Keys.Left)) { ChangeCurrentColor((currentColor + 2) % 3 + currentColor / 3 * 3); e.Handled = true; }
            if (e.KeyData == (Keys.Control | Keys.Right)) { ChangeCurrentColor((currentColor + 1) % 3 + currentColor / 3 * 3); e.Handled = true; }
            if (e.KeyData == (Keys.Control | Keys.Up)) { ChangeCurrentColor(currentColor % 3 + (currentColor / 3 + 6) % 7 * 3); e.Handled = true; }
            if (e.KeyData == (Keys.Control | Keys.Down)) { ChangeCurrentColor(currentColor % 3 + (currentColor / 3 + 1) % 7 * 3); e.Handled = true; }
            if (e.KeyData == (Keys.Alt | Keys.Left)) { ChangeStandardColor((standardColor + 2) % 3 + standardColor / 3 * 3); e.Handled = true; }
            if (e.KeyData == (Keys.Alt | Keys.Right)) { ChangeStandardColor((standardColor + 1) % 3 + standardColor / 3 * 3); e.Handled = true; }
            if (e.KeyData == (Keys.Alt | Keys.Up)) { ChangeStandardColor(standardColor % 3 + (standardColor / 3 + 5) % 6 * 3); e.Handled = true; }
            if (e.KeyData == (Keys.Alt | Keys.Down)) { ChangeStandardColor(standardColor % 3 + (standardColor / 3 + 1) % 6 * 3); e.Handled = true; }
        }

        private void dgv_field_KeyDown(object sender, KeyEventArgs e)
        {
            if (editMode != Mode.DEBUG_MODE)
            {
                if (e.KeyData == Keys.C || e.KeyData == Keys.T)
                {
                    ToggleEditMode();
                    e.Handled = true;
                }
                if (e.KeyData == (Keys.Control | Keys.A)) dgv_field.SelectAll();
                
                if (e.KeyData == (Keys.Control | Keys.B))
                {
                    Boolean bpChange = !breakPoints[dgv_field.CurrentCellAddress.X][dgv_field.CurrentCellAddress.Y];
                    foreach (DataGridViewCell item in dgv_field.SelectedCells)
                    {
                        ChangeBP(item.ColumnIndex, item.RowIndex, bpChange);
                    }
                }
                if (e.KeyData == (Keys.Control | Keys.Shift | Keys.B))
                {
                    foreach (DataGridViewCell item in dgv_field.SelectedCells)
                    {
                        ChangeBP(item.ColumnIndex, item.RowIndex, false);
                    }
                }
                if (e.KeyData == (Keys.Control | Keys.Z)) if (!tb_input.Focused) UndoHistory();
                if (e.KeyData == (Keys.Control | Keys.Y)) if (!tb_input.Focused) RedoHistory();
                if (e.KeyData == (Keys.Control | Keys.C)) CopyRect();
                if (e.KeyData == (Keys.Control | Keys.X)) CutRect();
                if (e.KeyData == (Keys.Control | Keys.V)) PasteRect();
                if (e.KeyData == Keys.Enter || e.KeyData == Keys.Space)
                {
                    e.Handled = true;
                    foreach (DataGridViewCell item in dgv_field.SelectedCells)
                    {
                        ChangeColor(item.ColumnIndex, item.RowIndex, currentColor);
                    }
                    AddHistory();
                }
                if (e.KeyData == Keys.Delete)
                {
                    e.Handled = true;
                    foreach (DataGridViewCell item in dgv_field.SelectedCells)
                    {
                        ChangeColor(item.ColumnIndex, item.RowIndex, PietColors.White);
                    }
                    AddHistory();
                }
            }
            if (editMode == Mode.DEBUG_MODE) {
                //if (e.KeyData == (Keys.Control | Keys.B))
                //{
                //    Boolean bpChange = !bp[dgv_field.CurrentCellAddress.X][dgv_field.CurrentCellAddress.Y];
                //    foreach (DataGridViewCell item in dgv_field.SelectedCells)
                //    {
                //        ChangeBP(item.ColumnIndex, item.RowIndex, bpChange);
                //    }
                //}
                //if (e.KeyData == (Keys.Control | Keys.Shift | Keys.B))
                //{
                //    foreach (DataGridViewCell item in dgv_field.SelectedCells)
                //    {
                //        ChangeBP(item.ColumnIndex, item.RowIndex, false);
                //    }
                //}
                e.Handled = true;
            }
        }

        private void dgv_field_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (editMode == Mode.SELECTOR_MODE) SelectColorBlock(e.ColumnIndex, e.RowIndex);
        }

        private void dgv_field_DragDrop(object sender, DragEventArgs e)
        {
            OpenFile(((string[])e.Data.GetData(DataFormats.FileDrop, false))[0]);
        }

        private void dgv_field_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string ext = Path.GetExtension(((string[])e.Data.GetData(DataFormats.FileDrop, false))[0]).ToLower();
                if(ext == ".png" || ext == ".bmp")
                    e.Effect = DragDropEffects.Copy;
            }
        }

        private void dgv_field_MouseWheel(object sender, MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                HandledMouseEventArgs wEventArgs = e as HandledMouseEventArgs;
                wEventArgs.Handled = true;
                ChangeCSize(codelSize - e.Delta / 60);
            }
        }

        #region Palette

        private void dgv_palette_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (Control.ModifierKeys == (Keys.Modifiers & Keys.None))
            {
                if(e.Button==MouseButtons.Left)if (e.ColumnIndex != 2 || e.RowIndex != 6) 
                    ChangeCurrentColor(e.ColumnIndex + e.RowIndex * 3);
                if (e.Button == MouseButtons.Right) if (e.RowIndex != 6)
                    ChangeStandardColor(e.ColumnIndex + e.RowIndex * 3);
            }
            else if (Control.ModifierKeys == (Keys.Modifiers & Keys.Control))
            {
                if (e.Button == MouseButtons.Left) RotateColor(e.ColumnIndex + e.RowIndex * 3);
            }
        }

        private void dgv_palette_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (editMode != Mode.DEBUG_MODE)
            {
                foreach (DataGridViewCell item in dgv_field.SelectedCells)
                {
                    ChangeColor(item.ColumnIndex, item.RowIndex, currentColor);
                }
                AddHistory();
            }
        }

        private void dgv_palette_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, e.PaintParts & ~DataGridViewPaintParts.Focus & ~DataGridViewPaintParts.SelectionBackground);
            if (e.ColumnIndex == currentColor % 3 && e.RowIndex == currentColor / 3)
            {
                ControlPaint.DrawBorder3D(e.Graphics, e.CellBounds, Border3DStyle.Etched);
            }
            if (e.ColumnIndex == 2 && e.RowIndex == 6)  
            {
                ControlPaint.DrawBorder3D(e.Graphics, e.CellBounds,Border3DStyle.Bump);
            }
            e.Handled = true;
        }

        #endregion

        #region Buttons

        private void btn_debug_Click(object sender, EventArgs e)
        {
            StartDebug();
        }

        private void btn_jump_Click(object sender, EventArgs e)
        {
            StartDebug(false, true);
        }

        private void btn_step_Click(object sender, EventArgs e)
        {
            StartDebug(true);
        }

        private void btn_reset_Click(object sender, EventArgs e)
        {
            ResetDebug();
        }

        private void btn_change_Click(object sender, EventArgs e)
        {
            ToggleEditMode();
        }

        #endregion

        #region Pen

        private void dgv_field_CellMouseMove(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (editMode == 0)
            {
                if (e.Button == MouseButtons.Left && penWriting)
                {
                    ChangeColor(e.ColumnIndex, e.RowIndex, currentColor);
                    history[history.Count - 1][e.ColumnIndex][e.RowIndex] = currentColor;
                    dgv_field[e.ColumnIndex, e.RowIndex].Selected = true;
                }
                else if (e.Button == MouseButtons.Right)
                {

                }
            }
        }

        private void dgv_field_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (editMode == 0)
            {
                if (e.Button == MouseButtons.Left)
                {
                penWriting = true;
                ChangeColor(e.ColumnIndex, e.RowIndex, currentColor);
                AddHistory();
                tm_pen.Enabled = true;
                }
            }
        }

        void StopPen()
        {
            penWriting = false;
            tm_pen.Enabled = false;
        }

        private void tm_pen_Tick(object sender, EventArgs e)
        {
            if ((Control.MouseButtons & MouseButtons.Left) != MouseButtons.Left)
            {
                StopPen();
            }
        }

        #endregion

        #region tsmi

        private void tsmi_CreateNew_Click(object sender, EventArgs e)
        {
            CreateNew();
        }

        private void tsmi_OpenFile_Click(object sender, EventArgs e)
        {
            OpenFile();
        }

        private void tsmi_Cut_Click(object sender, EventArgs e)
        {
            if (editMode != Mode.DEBUG_MODE) CutRect();
        }

        private void tsmi_Copy_Click(object sender, EventArgs e)
        {
            if (editMode != Mode.DEBUG_MODE) CopyRect();
        }

        private void tsmi_Paste_Click(object sender, EventArgs e)
        {
            if (editMode != Mode.DEBUG_MODE) PasteRect();
        }

        private void dgv_field_ColumnAdded(object sender, DataGridViewColumnEventArgs e)
        {
            e.Column.FillWeight = 0.001F;
        }

        private void tsmi_SaveFile_Click(object sender, EventArgs e)
        {
            SaveFile();
        }

        private void tsmi_SaveFileAs_Click(object sender, EventArgs e)
        {
            SaveFileAs();
        }

        private void tsmi_Quit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void tsmi_Undo_Click(object sender, EventArgs e)
        {
            if (editMode != Mode.DEBUG_MODE) UndoHistory();
        }

        private void tsmi_Redo_Click(object sender, EventArgs e)
        {
            if (editMode != Mode.DEBUG_MODE) RedoHistory();
        }

        private void tsmi_ChangeTool_Click(object sender, EventArgs e)
        {
            if (editMode != Mode.DEBUG_MODE) ToggleEditMode();
        }

        private void tsmi_ZoomIn_Click(object sender, EventArgs e)
        {
            ChangeCSize(codelSize + 1);
        }

        private void tsmi_ZoomOut_Click(object sender, EventArgs e)
        {
            ChangeCSize(codelSize - 1);
        }

        private void tsmi_ChangeCanvasSize_Click(object sender, EventArgs e)
        {
            if (editMode != Mode.DEBUG_MODE) ChangeSXSY();
        }

        private void tsmi_StartDebug_Click(object sender, EventArgs e)
        {
            StartDebug();
        }

        private void tsmi_JumpDebug_Click(object sender, EventArgs e)
        {
            StartDebug(false, true);
        }

        private void tsmi_StepDebug_Click(object sender, EventArgs e)
        {
            StartDebug(true);
        }

        private void tsmi_ResetDebug_Click(object sender, EventArgs e)
        {
            if (editMode == Mode.DEBUG_MODE) ResetDebug();
        }

        #endregion

        private void tm_status_Tick(object sender, EventArgs e)
        {
            string statusStr = "[status]\r\n";
            statusStr += "W: " + fieldWidth.ToString() + " H: " + fieldHeight.ToString() + "\r\nD^2: " + (fieldWidth * fieldWidth + fieldHeight * fieldHeight).ToString() + "\r\ncS: " + codelSize.ToString() + "\r\n\r\n";
            if (editMode == Mode.DEBUG_MODE)
            {
                statusStr +=
                    "Debugging...\r\nCommand:\r\n " + currentCommand +
                    "\r\ndp: " + directionPointerStrs[directionPointer] + " cc: " + codelChooserStrs[codelChooser] + "\r\nStep: " + stepCount.ToString();
            }
            else
            {
                statusStr += "Editing...\r\nTool: " + ((editMode == 0) ? "Pen" : "Selector");
            }
            lbl_status.Text = statusStr;
        }

        #region SelectAll

        private void tb_input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == (Keys.Control | Keys.A)) tb_input.SelectAll();
        }

        private void tb_output_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == (Keys.Control | Keys.A)) tb_output.SelectAll();
        }

        private void tb_stackbefore_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == (Keys.Control | Keys.A)) tb_stackbefore.SelectAll();
        }

        private void tb_stack_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == (Keys.Control | Keys.A)) tb_stack.SelectAll();
        }

        #endregion

        private void dgv_field_Enter(object sender, EventArgs e)
        {
            sc_field.Panel1.BackColor = Color.Pink;
        }

        private void dgv_field_Leave(object sender, EventArgs e)
        {
            sc_field.Panel1.BackColor = SystemColors.Control;
        }

        private void dgv_field_MouseEnter(object sender, EventArgs e)
        {
            dgv_field.Focus();
        }
    }
}
