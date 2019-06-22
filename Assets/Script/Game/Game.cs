using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RSG;
using Unity.UIWidgets.async;
using Unity.UIWidgets.ui;
using Unity.UIWidgets.widgets;
using UnityEngine;
using UnityEngine.Rendering;

namespace TetrisApp
{
    public enum GameStates
    {
        /// <summary>
        /// 未开始
        /// </summary>
        None,

        /// <summary>
        /// 正在进行
        /// </summary>
        Running,

        /// <summary>
        /// 暂停
        /// </summary>
        Paused,

        /// <summary>
        /// 重置
        /// </summary>
        Reset,

        /// <summary>
        /// 合并的状态
        /// </summary>
        Mixing,

        /// <summary>
        /// 消除
        /// </summary>
        Clear,

        /// <summary>
        /// 
        /// </summary>
        Drop
    }

    public class Game : StatefulWidget
    {
        public Widget Child;

        public Game(Widget child)
        {
            Child = child;
        }

        public override State createState()
        {
            return new GameController();
        }

        public static GameController of(BuildContext context)
        {
            return context.ancestorStateOfType(new TypeMatcher<GameController>()) as GameController;
        }
    }

    public class GameController : State<Game>
    {
        private Block mCurrent;

        private GameStates mStates = GameStates.None;

        private int mLines = 0;

        private int mPoints = 0;

        private Timer mAutoFallTimer = null;

        private Block mNext = Block.Next;

        private int mLevel = 1;

        Block GetNext()
        {
            var next = mNext;
            mNext = Block.Next;
            return next;
        }

        public void Reset()
        {
            if (mStates == GameStates.None)
            {
                StartGame();
                return;
            }

            if (mStates == GameStates.Reset)
            {
                return;
            }

            mStates = GameStates.Reset;


            Window.instance.startCoroutine(DoResetAnimation());
        }

        IEnumerator DoResetAnimation()
        {
            var line = AppConstants.GAME_PAD_10x20_H;

            do
            {
                line--;
                for (var i = 0; i < AppConstants.GAME_PAD_10X20_W; i++)
                {
                    mData[line][i] = 1;
                }

                setState(() => { });

                yield return new WaitForSeconds(0.05f);
            } while (line != 0);

            mCurrent = null;
            GetNext();

            mPoints = 0;
            mLines = 0;

            do
            {
                for (var i = 0; i < AppConstants.GAME_PAD_10X20_W; i++)
                {
                    mData[line][i] = 0;
                }


                setState(() => { });
                line++;
                yield return new WaitForSeconds(0.05f);
            } while (line != AppConstants.GAME_PAD_10x20_H);

            this.setState(() => { mStates = GameStates.None; });
        }

        public void PauseOrResume()
        {
            if (mStates == GameStates.Running)
            {
                mStates = GameStates.Paused;
            }
            else if (mStates == GameStates.Paused)
            {
                mStates = GameStates.Running;
            }

            this.setState(() => { });
        }

        public void SoundSwitch()
        {
            Debug.Log("SoundSwitch");
        }

        public void Drop()
        {
            if (mStates == GameStates.Running && mCurrent != null)
            {
                for (var i = 0; i < 20; i++)
                {
                    var next = mCurrent.Down(i + 1);

                    if (!next.IsValidateInData(mData))
                    {
                        mCurrent = mCurrent.Down(i);

                        this.setState(() => { });
                        mStates = GameStates.Drop;

                        Promise.Delayed(TimeSpan.FromMilliseconds(100))
                            .Then(() => { MixCurrentBlockIntoData(); });

                        break;
                    }
                }
            }
            else if (mStates == GameStates.Paused || mStates == GameStates.None)
            {
                StartGame();
            }
        }

        void StartGame()
        {
            if (mStates == GameStates.Running && mAutoFallTimer == null)
            {
                return;
            }

            mStates = GameStates.Running;

            AutoFall(true);

            setState(() => { });
        }

        void AutoFall(bool enable)
        {
            if (!enable && mAutoFallTimer != null)
            {
                mAutoFallTimer?.cancel();
                mAutoFallTimer = null;
            }
            else if (enable)
            {
                mAutoFallTimer?.cancel();
                mCurrent = mCurrent ?? GetNext();
                mAutoFallTimer = Window.instance.periodic(AppConstants.SPEED[mLevel - 1], () => { Down(); });
            }
        }

        public void Rotate()
        {
            if (mStates == GameStates.Running)
            {
                var next = mCurrent.Rotate();

                if (next.IsValidateInData(mData))
                {
                    mCurrent = next;
                }

                setState(() => { });
            }
        }

        public void Down()
        {
            if (mStates == GameStates.Running)
            {
                var next = mCurrent.Down();

                if (next.IsValidateInData(mData))
                {
                    mCurrent = next;
                }
                else
                {
                    // 进行积累
                    MixCurrentBlockIntoData();
                }

                setState(() => { });
            }
        }

        public void Left()
        {
            if (mStates == GameStates.Running && mCurrent != null)
            {
                var next = mCurrent.Left();

                if (next.IsValidateInData(mData))
                {
                    mCurrent = next;
                }

            } else if (mStates == GameStates.None && mLevel > 1)
            {
                mLevel--;
            }
            
            setState(() => { });
        }

        public void Right()
        {
            if (mStates == GameStates.Running && mCurrent != null)
            {
                var next = mCurrent.Right();

                if (next.IsValidateInData(mData))
                {
                    mCurrent = next;
                }

            } else if (mStates == GameStates.None && mLevel < 6)
            {
                mLevel++;
            }
            setState(() => { });
        }

        private List<List<int>> GetMixedData()
        {
            var mixed = new List<List<int>>();

            for (var rowIndex = 0; rowIndex < mData.Count; rowIndex++)
            {
                var line = mData[rowIndex];

                var lineDatas = new List<int>();

                for (var colIndex = 0; colIndex < line.Count; colIndex++)
                {
                    var brickData = (mCurrent == null || mCurrent.Get(rowIndex, colIndex) != 1) ? line[colIndex] : 1;
                    lineDatas.Add(brickData);
                }

                mixed.Add(lineDatas);
            }

            return mixed;
        }


        void MixCurrentBlockIntoData()
        {
            mData = GetMixedData();

            GetNext();

            var clearLines = new List<int>();

            for (var i = 0; i < mData.Count; i++)
            {
                if (mData[i].All(brickData => brickData == 1))
                {
                    clearLines.Add(i);
                }
            }

            if (clearLines.Count > 0)
            {
                // 进行消除操作
                Debug.Log("进行消除操作");

                clearLines.Reverse();

                clearLines.ForEach(lineIndex => mData.RemoveAt(lineIndex));

                clearLines.ForEach(__ => mData.Insert(0, Enumerable.Range(0, 10).Select(_ => 0).ToList()));

                mLines += clearLines.Count;
                mPoints += Rules.PointsForClearLines(clearLines.Count);

                Debug.Log($"Lines:{mLines} Points:{mPoints}");
            }

            mStates = GameStates.Running;

            mCurrent = null;

            if (mData[0].Contains(1))
            {
                Reset();
            }
            else
            {
                StartGame();
            }
        }

        private List<List<int>> mData = new List<List<int>>
        {
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0}, // 0
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0}, // 1
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0}, // 2
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0}, // 3
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0}, // 4
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new List<int> {0, 0, 0, 0, 0, 0, 0, 0, 0, 0}
        };

        public override Widget build(BuildContext context)
        {
            return new GameState(
                data: GetMixedData(),
                level:mLevel,
                states: mStates,
                clearLines: mLines,
                points: mPoints,
                next: mNext,
                child: widget.Child
            );
        }
    }

    public class GameState : InheritedWidget
    {
        public int ClearLines = 0;
        public int Points     = 0;

        public Block Next = null;

        public GameStates States;

        public int Level = 1;

        public GameState(List<List<int>> data,int level, GameStates states, int clearLines, int points, Block next,
            Widget child) : base(
            child: child)
        {
            Data = data;
            ClearLines = clearLines;
            Points = points;
            Next = next;
            States = states;
            Level = level;
        }

        public List<List<int>> Data { get; }


        public static GameState of(BuildContext context)
        {
            return context.inheritFromWidgetOfExactType(typeof(GameState)) as GameState;
        }

        public override bool updateShouldNotify(InheritedWidget oldWidget)
        {
            return true;
        }
    }
}