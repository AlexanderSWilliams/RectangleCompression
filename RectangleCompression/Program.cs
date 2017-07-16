using RectangleCompression.IEnumerableExtensions;
using RectangleCompression.IListExtensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace RectangleCompression
{
    internal class Program
    {
        public static Dictionary<int, RectangleCut> CutDict = new Dictionary<int, RectangleCut>();
        public static Dictionary<int, RectangleCut> DemoCutDict = null;

        public enum Placement : byte
        {
            Under,
            Adjacent
        }

        public static RectangleCut BoxCutFromId(int id)
        {
            RectangleCut Cut;
            if (CutDict.TryGetValue(id, out Cut))
                return Cut;

            var Result = new RectangleCut { Cuts = DemoCutDict[id].Cuts.Where(x => x != 0).ToList() };
            CutDict.Add(id, Result);
            return Result;
        }

        public static List<Page> CalculatePages(Page page, InOutRect rectangle, Placement placement, PageSetting setting)
        {
            var Pages = new List<Page> { ClonePage(page) };
            var Page = Pages[0];
            var LastColumn = Page.Columns.Any() ? Page.Columns.Last() : null;
            var RemainingHeight = 0;

            if (placement == Placement.Under)
            {
                var LastRectangle = LastColumn?.Last().Rectangle;
                var Y = (LastRectangle?.Height + LastRectangle?.Y + setting.Padding ?? 0);
                if ((LastColumn?[0].Rectangle.X ?? 0) + rectangle.Rectangle.Width > setting.Width)
                    return null;

                if (Y + rectangle.Rectangle.Height > setting.Height)
                {
                    var Cuts = BoxCutFromId(rectangle.Id).Cuts;
                    var SplitHeight = PreviousSplitHeight(Page, rectangle.Id);
                    var ValidCuts = Cuts.Where(x => Y + x - SplitHeight <= setting.Height);
                    if (!ValidCuts.Any())
                        return null;
                    var MaxCut = ValidCuts.Max();
                    Page.Columns.Last().Add(new InOutRect
                    {
                        Id = rectangle.Id,
                        Rectangle = new Rectangle { X = (LastColumn?[0].Rectangle.X ?? 0), Y = Y, Width = rectangle.Rectangle.Width, Height = MaxCut - SplitHeight }
                    });

                    var NextHeight = Math.Min(rectangle.Rectangle.Height - MaxCut, setting.Height);
                    RemainingHeight = rectangle.Rectangle.Height - MaxCut - NextHeight;

                    var X = Page.Columns.Last().Max(x => x.Rectangle.X + x.Rectangle.Width) + setting.Spacing;
                    if (X + rectangle.Rectangle.Width > setting.Width)
                    {
                        Pages.Add(new Page
                        {
                            Columns = new List<List<InOutRect>> { new List<InOutRect> {new InOutRect {
                                    Id = rectangle.Id,
                                    Rectangle = new Rectangle { X = 0, Y = 0, Width = rectangle.Rectangle.Width, Height = NextHeight }
                                }}}
                        });
                    }
                    else
                        Page.Columns.Add(new List<InOutRect> { new InOutRect {
                            Id = rectangle.Id,
                            Rectangle = new Rectangle { X = X, Y = 0, Width = rectangle.Rectangle.Width, Height = NextHeight }
                        }});
                }
                else
                {
                    var UnderRectangle = new InOutRect
                    {
                        Id = rectangle.Id,
                        Rectangle = new Rectangle { X = (LastColumn?[0].Rectangle.X ?? 0), Y = Y, Width = rectangle.Rectangle.Width, Height = rectangle.Rectangle.Height }
                    };
                    if (!Page.Columns.Any())
                        Page.Columns.Add(new List<InOutRect> { UnderRectangle });
                    else
                        Page.Columns.Last().Add(UnderRectangle);
                }
            }
            else
            {
                var X = Page.Columns.Last().Max(x => x.Rectangle.X + x.Rectangle.Width) + setting.Spacing;
                if (X + rectangle.Rectangle.Width > setting.Width)
                    return null;

                if (rectangle.Rectangle.Height > setting.Height)
                {
                    var Cuts = BoxCutFromId(rectangle.Id).Cuts;
                    var SplitHeight = PreviousSplitHeight(Page, rectangle.Id);
                    var ValidCuts = Cuts.Where(x => x - SplitHeight <= setting.Height);
                    if (!ValidCuts.Any())
                        return null;
                    var MaxCut = ValidCuts.Max();

                    Page.Columns.Add(new List<InOutRect>{new InOutRect
                    {
                        Id = rectangle.Id,
                        Rectangle = new Rectangle { X = X, Y = 0, Width = rectangle.Rectangle.Width, Height = MaxCut - SplitHeight }
                    }});

                    var NextHeight = Math.Min(rectangle.Rectangle.Height - MaxCut, setting.Height);
                    RemainingHeight = rectangle.Rectangle.Height - MaxCut - NextHeight;

                    X = X + rectangle.Rectangle.Width + setting.Spacing;
                    if (X + rectangle.Rectangle.Width > setting.Width)
                        Pages.Add(new Page
                        {
                            Columns = new List<List<InOutRect>> { new List<InOutRect> {new InOutRect {
                                Id = rectangle.Id,
                                Rectangle = new Rectangle { X = 0, Y = 0, Width = rectangle.Rectangle.Width, Height = NextHeight }
                            }}}
                        });
                    else
                        Page.Columns.Add(new List<InOutRect> { new InOutRect {
                            Id = rectangle.Id,
                            Rectangle = new Rectangle { X = X, Y = 0, Width = rectangle.Rectangle.Width, Height = NextHeight }
                        }});
                }
                else
                {
                    Page.Columns.Add(new List<InOutRect>{new InOutRect
                    {
                        Id = rectangle.Id,
                        Rectangle = new Rectangle { X = X, Y = 0, Width = rectangle.Rectangle.Width, Height = rectangle.Rectangle.Height }
                    }});
                }
            }

            if (RemainingHeight != 0)
            {
                return Pages.Take(Pages.Count - 1).Concat(CalculatePages(Pages.Last(), new InOutRect
                {
                    Id = rectangle.Id,
                    Rectangle = new Rectangle
                    {
                        Width = rectangle.Rectangle.Width,
                        Height = RemainingHeight
                    }
                }, Placement.Adjacent, setting)).ToList();
            }

            return Pages;
        }

        public static Page ClonePage(Page page)
        {
            return new Page { Columns = page.Columns.Select(y => y.ToList()).ToList() };
        }

        public static Page Compress(Page page, PageSetting setting, int numberOfColumns)
        {
            var Result = ClonePage(page);
            var Heights = Result.Columns.Select(x => x.Last().Rectangle).Select(x => x.Y + x.Height).ToArray();
            var AverageHeight = (Heights.Sum() + (numberOfColumns - Result.Columns.Count) * setting.Padding) * 1.0 / ((double)numberOfColumns);

            for (var CurrentIndex = 0; CurrentIndex < Result.Columns.Count; CurrentIndex++)
            {
                do
                {
                    var CurrentColumn = Result.Columns[CurrentIndex];
                    var CurrentHeight = CurrentColumn.Last().Rectangle.Y + CurrentColumn.Last().Rectangle.Height; // recalculate b/c they can change
                    var CurrentDiff = Math.Abs(CurrentHeight - AverageHeight);

                    if (CurrentHeight == AverageHeight || (CurrentHeight < AverageHeight && CurrentIndex + 1 == Result.Columns.Count))
                        break;
                    var SplitRectPage = CurrentHeight < AverageHeight ? SplitTopRectangle(Result, setting, CurrentIndex, AverageHeight) :
                        SplitBottomRectangle(Result, setting, CurrentIndex, AverageHeight);
                    var FullyMovedRectPage = CurrentHeight < AverageHeight ? FullyMoveTopRectangle(Result, setting, CurrentIndex) :
                        FullyMoveBottomRectangle(Result, setting, CurrentIndex);

                    var PartialRectangle = SplitRectPage != null ? SplitRectPage.Columns[CurrentIndex].Last() : null;
                    var FullRectangle = FullyMovedRectPage != null ? FullyMovedRectPage.Columns[CurrentIndex].Last() : null;
                    var PartialRectangleDiff = PartialRectangle != null ? Math.Abs(AverageHeight - PartialRectangle.Rectangle.Y - PartialRectangle.Rectangle.Height) : int.MaxValue;
                    var FullRectangleDiff = FullRectangle != null ? Math.Abs(AverageHeight - FullRectangle.Rectangle.Y - FullRectangle.Rectangle.Height) : int.MaxValue;

                    if (CurrentDiff <= PartialRectangleDiff && CurrentDiff <= FullRectangleDiff)
                        break;
                    if (PartialRectangleDiff < FullRectangleDiff)
                    {
                        Result = SplitRectPage;
                        break;
                    }
                    else
                        Result = FullyMovedRectPage;
                } while (CurrentIndex < Result.Columns.Count);
            }

            return Result;
        }

        public static Page Compress(Page page, PageSetting setting)
        {
            return Enumerable.Range(1, int.MaxValue).Select(x => new
            {
                Page = Compress(page, setting, x),
                NumberOfColumns = x
            })
            .TakeWhile(x => x.Page.Columns.Count >= x.NumberOfColumns)
            .Select(x => new
            {
                x.Page,
                Heights = x.Page.Columns.Select(y => y.Last().Rectangle).Select(y => y.Height + y.Y),
                AverageHeight = x.Page.Columns.Select(y => y.Last().Rectangle).Select(y => y.Height + y.Y).Average()
            })
            .MinBy(x => x.Heights.Max())
            .MinBy(x => x.Heights.Max(y => Math.Abs(x.AverageHeight - y))).First().Page;
        }

        public static Dictionary<int, RectangleCut> CutoffsFromFile(string inputFilePath)
        {
            var Input = System.IO.File.ReadAllText(inputFilePath);

            return Input.Split(new[] { "\r\n" }, StringSplitOptions.None).Where(x => x.Length != 0)
                .Select(x => x.Split(new[] { '\t' }))
                .Select((x, i) => new
                {
                    Id = i + 1,
                    RectangleCut = new RectangleCut
                    {
                        Cuts = x[1].Split(new[] { ',' }).Select(y => int.Parse(y)).ToList()
                    }
                }).ToDictionary(x => x.Id, x => x.RectangleCut);
        }

        public static Page FullyMoveBottomRectangle(Page page, PageSetting setting, int currentIndex)
        {
            var Result = ClonePage(page);
            var CurrentColumn = Result.Columns[currentIndex];
            if (Result.Columns.Count == currentIndex + 1)
                Result.Columns.Add(new List<InOutRect> { });

            var NextColumn = Result.Columns[currentIndex + 1];
            var BottomRectangle = CurrentColumn.Last();
            var TopRectangle = NextColumn.Any() ? NextColumn[0] : null;

            var WasSplit = TopRectangle != null && BottomRectangle.Id == TopRectangle.Id;
            if (WasSplit)
            {
                NextColumn[0] = new InOutRect
                {
                    Id = TopRectangle.Id,
                    Rectangle = new Rectangle
                    {
                        Width = TopRectangle.Rectangle.Width,
                        Height = BottomRectangle.Rectangle.Height + TopRectangle.Rectangle.Height,
                        X = TopRectangle.Rectangle.X,
                        Y = TopRectangle.Rectangle.Y
                    }
                };

                CurrentColumn.RemoveAt(CurrentColumn.Count - 1);
                UpdateYCoordinates(Result, setting, currentIndex + 1);
                UpdateXCoordinates(Result, setting);

                return IsValidPage(Result, setting) ? Result : null;
            }

            var X = CurrentColumn.Max(x => x.Rectangle.X + x.Rectangle.Width) + setting.Spacing;
            NextColumn.Insert(0, new InOutRect
            {
                Id = BottomRectangle.Id,
                Rectangle = new Rectangle
                {
                    Width = BottomRectangle.Rectangle.Width,
                    Height = BottomRectangle.Rectangle.Height,
                    X = X,
                    Y = 0
                }
            });

            CurrentColumn.RemoveAt(CurrentColumn.Count - 1);
            UpdateYCoordinates(Result, setting, currentIndex + 1);
            UpdateXCoordinates(Result, setting);

            return IsValidPage(Result, setting) ? Result : null;
        }

        public static Page FullyMoveTopRectangle(Page page, PageSetting setting, int currentIndex)
        {
            var Result = ClonePage(page);
            var CurrentColumn = Result.Columns[currentIndex];
            var NextColumn = Result.Columns[currentIndex + 1];
            var BottomRectangle = CurrentColumn.Last();
            var TopRectangle = NextColumn[0];

            var WasSplit = BottomRectangle.Id == TopRectangle.Id;
            if (WasSplit)
            {
                CurrentColumn[CurrentColumn.Count - 1] = new InOutRect
                {
                    Id = BottomRectangle.Id,
                    Rectangle = new Rectangle
                    {
                        Width = BottomRectangle.Rectangle.Width,
                        Height = BottomRectangle.Rectangle.Height + TopRectangle.Rectangle.Height,
                        X = BottomRectangle.Rectangle.X,
                        Y = BottomRectangle.Rectangle.Y
                    }
                };

                NextColumn.RemoveAt(0);
                UpdateYCoordinates(Result, setting, currentIndex + 1);
                UpdateXCoordinates(Result, setting);
                return IsValidPage(Result, setting) ? Result : null;
            }

            CurrentColumn.Add(new InOutRect
            {
                Id = TopRectangle.Id,
                Rectangle = new Rectangle
                {
                    Width = TopRectangle.Rectangle.Width,
                    Height = TopRectangle.Rectangle.Height,
                    X = BottomRectangle.Rectangle.X,
                    Y = BottomRectangle.Rectangle.Y + BottomRectangle.Rectangle.Height + setting.Padding
                }
            });

            NextColumn.RemoveAt(0);
            UpdateYCoordinates(Result, setting, currentIndex + 1);
            UpdateXCoordinates(Result, setting);
            return IsValidPage(Result, setting) ? Result : null;
        }

        public static bool IsHorizontallyValid(List<InOutRect> rectangles, int width)
        {
            return rectangles.All(x => x.Rectangle.Width <= width);
        }

        public static bool IsValidPage(Page page, PageSetting setting)
        {
            if (page.Columns.Last().Max(x => x.Rectangle.X + x.Rectangle.Width) > setting.Width)
                return false;
            if (page.Columns.Select(x => x.Last()).Max(x => x.Rectangle.Height + x.Rectangle.Y) > setting.Height)
                return false;
            return true;
        }

        public static bool IsVerticallyValid(InOutRect rectangle, int height)
        {
            var Cuts = BoxCutFromId(rectangle.Id).Cuts;
            if (!Cuts.Any())
                return rectangle.Rectangle.Height <= height;

            return rectangle.Rectangle.Height - Cuts.Last() <= height &&
                Enumerable.Range(0, Cuts.Count - 1).All(x => Cuts[x + 1] - Cuts[x] <= height);
        }

        public static List<Page> MaxRectanglesOnPage(List<InOutRect> rectangles, PageSetting setting)
        {
            var ValidPlacements = TreeOfValidPlacements(rectangles, setting);
            var Frontier = PlacementFrontier(ValidPlacements);

            return Frontier.MaxBy(x => x[0].Columns.Last().Last().Id).ToList()
                .MinBy(x => x.Count != 1 ? x[1].Columns[0][0].Rectangle.Height : 0).ToList()
                .MinBy(x => x[0].Columns.Max(y => y.Last().Rectangle.Y + y.Last().Rectangle.Height))
                .MaxBy(x => x[0].Columns.Count).First();
        }

        public static void PagesToOutputFile(List<Page> pages, string pathToOutputFile)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < pages.Count; i++)
            {
                if (i != 0)
                    sb.Append("\r\n");

                var Page = pages[i];
                sb.Append("area" + (i + 1) + ":\r\n");
                foreach (var Column in Page.Columns)
                    foreach (var Rect in Column)
                        sb.Append(Rect.Id + " " + Rect.Rectangle.X + "," + Rect.Rectangle.Y + "," + Rect.Rectangle.Width + "," + Rect.Rectangle.Height + "\r\n");
            }

            File.WriteAllText(pathToOutputFile, sb.ToString());
        }

        public static List<List<Page>> PlacementFrontier(PlacementTree parentNode)
        {
            return PlacementFrontier(parentNode, new List<List<Page>>());
        }

        public static List<List<Page>> PlacementFrontier(PlacementTree parentNode, List<List<Page>> result)
        {
            if (parentNode.Under == null && parentNode.Adjacent == null && parentNode.Pages != null)
                result.Add(parentNode.Pages);

            if (parentNode.Under != null)
                PlacementFrontier(parentNode.Under, result);

            if (parentNode.Adjacent != null)
                PlacementFrontier(parentNode.Adjacent, result);

            return result;
        }

        public static int PreviousSplitHeight(Page page, int rectangleID, int? columnIndex = null)
        {
            var Columns = columnIndex != null ? page.Columns.Take(columnIndex.Value) : page.Columns;
            var PreviousRectangles = Columns.SelectMany(x => x)
                .SkipWhile(x => x.Id != rectangleID)
                .TakeWhile(x => x.Id == rectangleID).ToArray();
            return PreviousRectangles.Any() ? PreviousRectangles.Sum(x => x.Rectangle.Height) : 0;
        }

        public static List<InOutRect> RectanglesFromFile(string inputFilePath)
        {
            var Input = System.IO.File.ReadAllText(inputFilePath);

            return Input.Split(new[] { "\r\n" }, StringSplitOptions.None).Where(x => x.Length != 0)
                .Select(x => x.Split(new[] { '\t' }))
                .Select(x => x[0].Split(new[] { ',' }).Select(y => int.Parse(y)).ToArray())
                .Select((x, i) => new InOutRect
                {
                    Rectangle = new Rectangle { Width = x[2], Height = x[3] },
                    Id = i + 1
                })
                .ToList();
        }

        public static Page SplitBottomRectangle(Page page, PageSetting setting, int currentIndex, double desiredHeight)
        {
            var Result = ClonePage(page);
            var CurrentColumn = Result.Columns[currentIndex];
            if (Result.Columns.Count == currentIndex + 1)
                Result.Columns.Add(new List<InOutRect> { });

            var NextColumn = Result.Columns[currentIndex + 1];
            var BottomRectangle = CurrentColumn.Last();
            var TopRectangle = NextColumn.Any() ? NextColumn[0] : null;

            var Cuts = BoxCutFromId(BottomRectangle.Id).Cuts;
            if (!Cuts.Any())
                return null;

            var CurrentHeight = BottomRectangle.Rectangle.Height + BottomRectangle.Rectangle.Y;
            var CurrentDiff = Math.Abs(desiredHeight - CurrentHeight);
            var SplitHeight = PreviousSplitHeight(Result, BottomRectangle.Id, currentIndex);
            var BestCut = Cuts.MinBy(x => Math.Abs(desiredHeight - (BottomRectangle.Rectangle.Y + x - SplitHeight)));
            if (BestCut.Key > CurrentDiff)
                return null;
            var BestCutHeight = BestCut.First() - SplitHeight;

            var WasSplit = TopRectangle != null && BottomRectangle.Id == TopRectangle.Id;
            if (WasSplit)
            {
                CurrentColumn[CurrentColumn.Count - 1] = new InOutRect
                {
                    Id = BottomRectangle.Id,
                    Rectangle = new Rectangle
                    {
                        Width = BottomRectangle.Rectangle.Width,
                        Height = BestCutHeight,
                        X = BottomRectangle.Rectangle.X,
                        Y = BottomRectangle.Rectangle.Y
                    }
                };

                NextColumn[0] = new InOutRect
                {
                    Id = TopRectangle.Id,
                    Rectangle = new Rectangle
                    {
                        Width = TopRectangle.Rectangle.Width,
                        Height = BottomRectangle.Rectangle.Height + TopRectangle.Rectangle.Height - BestCutHeight,
                        X = TopRectangle.Rectangle.X,
                        Y = TopRectangle.Rectangle.Y
                    }
                };

                UpdateYCoordinates(Result, setting, currentIndex + 1);
                UpdateXCoordinates(Result, setting);
                return IsValidPage(Result, setting) ? Result : null;
            }

            var X = CurrentColumn.Max(x => x.Rectangle.X + x.Rectangle.Width) + setting.Spacing;
            NextColumn.Insert(0, new InOutRect
            {
                Id = BottomRectangle.Id,
                Rectangle = new Rectangle
                {
                    Width = BottomRectangle.Rectangle.Width,
                    Height = BottomRectangle.Rectangle.Height - BestCutHeight,
                    X = X,
                    Y = 0
                }
            });

            CurrentColumn[CurrentColumn.Count - 1] = new InOutRect
            {
                Id = BottomRectangle.Id,
                Rectangle = new Rectangle
                {
                    Width = BottomRectangle.Rectangle.Width,
                    Height = BestCutHeight,
                    X = BottomRectangle.Rectangle.X,
                    Y = BottomRectangle.Rectangle.Y
                }
            };

            UpdateYCoordinates(Result, setting, currentIndex + 1);
            UpdateXCoordinates(Result, setting);
            return IsValidPage(Result, setting) ? Result : null;
        }

        public static Page SplitTopRectangle(Page page, PageSetting setting, int currentIndex, double desiredHeight)
        {
            var Result = ClonePage(page);
            var CurrentColumn = Result.Columns[currentIndex];

            var NextColumn = Result.Columns[currentIndex + 1];
            var BottomRectangle = CurrentColumn.Last();
            var TopRectangle = NextColumn[0];

            var Cuts = BoxCutFromId(TopRectangle.Id).Cuts;
            if (!Cuts.Any())
                return null;
            var SplitHeight = PreviousSplitHeight(Result, TopRectangle.Id, currentIndex);

            var CurrentHeight = BottomRectangle.Rectangle.Height + BottomRectangle.Rectangle.Y;
            var CurrentDiff = Math.Abs(desiredHeight - CurrentHeight);
            var WasSplit = BottomRectangle.Id == TopRectangle.Id;
            if (WasSplit)
            {
                var BestSplitCut = Cuts.MinBy(x => Math.Abs(desiredHeight - (BottomRectangle.Rectangle.Y + x - SplitHeight)));
                if (BestSplitCut.Key > CurrentDiff)
                    return null;
                var BestSplitCutHeight = BestSplitCut.First() - SplitHeight;

                CurrentColumn[CurrentColumn.Count - 1] = new InOutRect
                {
                    Id = BottomRectangle.Id,
                    Rectangle = new Rectangle
                    {
                        Width = BottomRectangle.Rectangle.Width,
                        Height = BestSplitCutHeight,
                        X = BottomRectangle.Rectangle.X,
                        Y = BottomRectangle.Rectangle.Y
                    }
                };

                NextColumn[0] = new InOutRect
                {
                    Id = TopRectangle.Id,
                    Rectangle = new Rectangle
                    {
                        Width = TopRectangle.Rectangle.Width,
                        Height = BottomRectangle.Rectangle.Height + TopRectangle.Rectangle.Height - BestSplitCutHeight,
                        X = TopRectangle.Rectangle.X,
                        Y = TopRectangle.Rectangle.Y
                    }
                };

                UpdateYCoordinates(Result, setting, currentIndex + 1);
                UpdateXCoordinates(Result, setting);
                return IsValidPage(Result, setting) ? Result : null;
            }

            var BestCut = Cuts.MinBy(x => Math.Abs(desiredHeight - (BottomRectangle.Rectangle.Y + x - SplitHeight + setting.Padding)));
            if (BestCut.Key > CurrentDiff)
                return null;
            var BestCutHeight = BestCut.First() - SplitHeight;

            CurrentColumn.Add(new InOutRect
            {
                Id = TopRectangle.Id,
                Rectangle = new Rectangle
                {
                    Width = TopRectangle.Rectangle.Width,
                    Height = BestCutHeight,
                    X = BottomRectangle.Rectangle.X,
                    Y = BottomRectangle.Rectangle.Y + BottomRectangle.Rectangle.Height + setting.Padding
                }
            });

            NextColumn[0] = new InOutRect
            {
                Id = TopRectangle.Id,
                Rectangle = new Rectangle
                {
                    Width = TopRectangle.Rectangle.Width,
                    Height = TopRectangle.Rectangle.Height - BestCutHeight,
                    X = TopRectangle.Rectangle.X,
                    Y = TopRectangle.Rectangle.Y
                }
            };

            UpdateYCoordinates(Result, setting, currentIndex + 1);
            UpdateXCoordinates(Result, setting);
            return IsValidPage(Result, setting) ? Result : null;
        }

        public static PlacementTree TreeOfValidPlacements(List<InOutRect> rectangles, PageSetting setting)
        {
            var InitialRectangle = rectangles[0];
            if (!IsVerticallyValid(InitialRectangle, setting.Height))
                throw new Exception("There is a rectangle that does not vertically fit the dimensions provided.");
            return TreeOfValidPlacements(new PlacementTree(), rectangles, setting, Placement.Under, rectangles[0], 1);
        }

        public static PlacementTree TreeOfValidPlacements(PlacementTree parentNode, List<InOutRect> rectangles, PageSetting setting,
            Placement placement, InOutRect rect, int index)
        {
            var Pages = CalculatePages(parentNode.Pages[0], rect, placement, setting);
            if (Pages == null)
                return null;

            var Placements = parentNode.Placements.ToList();
            Placements.Add(placement);

            var currentNode = new PlacementTree(Placements, Pages);
            if (Pages.Count == 1 && index < rectangles.Count)
            {
                currentNode.Under = TreeOfValidPlacements(currentNode, rectangles, setting, Placement.Under,
                    rectangles[index], index + 1);

                // To minimize expoential search time, we limit the adjacent searches to when the width would increase
                if (currentNode.Under == null || currentNode.Under.Pages[0].Columns.Last().Max(x => x.Rectangle.Width) < rect.Rectangle.Width)
                {
                    currentNode.Adjacent = TreeOfValidPlacements(currentNode, rectangles, setting, Placement.Adjacent,
                        rectangles[index], index + 1);
                }
            }
            return currentNode;
        }

        public static void UpdateXCoordinates(Page page, PageSetting setting)
        {
            var ColumnWidths = page.Columns.Select(x => x.Any() ? x.Max(y => y.Rectangle.Width) : 0).ToArray();

            var X = 0;
            for (var i = 1; i < page.Columns.Count; i++)
            {
                var CurrentColumn = page.Columns[i];
                X = X + ColumnWidths[i - 1] + setting.Spacing;
                for (var j = 0; j < CurrentColumn.Count; j++)
                {
                    var CurrentRectangle = CurrentColumn[j];
                    CurrentColumn[j] = new InOutRect
                    {
                        Id = CurrentRectangle.Id,
                        Rectangle = new Rectangle
                        {
                            Width = CurrentRectangle.Rectangle.Width,
                            Height = CurrentRectangle.Rectangle.Height,
                            X = X,
                            Y = CurrentRectangle.Rectangle.Y,
                        }
                    };
                }
            }
        }

        public static void UpdateYCoordinates(Page page, PageSetting setting, int columnIndex)
        {
            var CollapsedColumnIndex = page.Columns.IndexOf(x => !x.Any());
            if (CollapsedColumnIndex != -1)
            {
                page.Columns.RemoveAt(CollapsedColumnIndex);
                if (CollapsedColumnIndex < columnIndex)
                {
                    if (columnIndex > 1)
                        UpdateYCoordinates(page, setting, columnIndex - 1);
                }
                else if (CollapsedColumnIndex > columnIndex)
                    UpdateYCoordinates(page, setting, columnIndex);
                return;
            }

            var CurrentColumn = page.Columns[columnIndex];
            var SplitRectanges = CurrentColumn.GroupBy(x => x.Id).Where(x => x.Count() == 2);
            foreach (var split in SplitRectanges)
            {
                var SplitIndex = CurrentColumn.IndexOf(x => x.Id == split.Key);
                CurrentColumn[SplitIndex] = new InOutRect
                {
                    Id = CurrentColumn[SplitIndex].Id,
                    Rectangle = new Rectangle
                    {
                        Width = CurrentColumn[SplitIndex].Rectangle.Width,
                        Height = CurrentColumn[SplitIndex].Rectangle.Height + CurrentColumn[SplitIndex + 1].Rectangle.Height,
                        X = CurrentColumn[SplitIndex].Rectangle.X,
                        Y = 0
                    }
                };
                CurrentColumn.RemoveAt(SplitIndex + 1);
            }

            var Y = 0;
            for (var i = 0; i < CurrentColumn.Count; i++)
            {
                var CurrentRectangle = CurrentColumn[i];
                if (Y + CurrentRectangle.Rectangle.Height <= setting.Height)
                {
                    CurrentColumn[i] = new InOutRect
                    {
                        Id = CurrentRectangle.Id,
                        Rectangle = new Rectangle
                        {
                            Width = CurrentRectangle.Rectangle.Width,
                            Height = CurrentRectangle.Rectangle.Height,
                            X = CurrentRectangle.Rectangle.X,
                            Y = Y,
                        }
                    };
                    Y = Y + CurrentRectangle.Rectangle.Height + setting.Padding;
                }
                else
                {
                    if (columnIndex == page.Columns.Count - 1)
                        page.Columns.Add(new List<InOutRect>());

                    var Cuts = BoxCutFromId(CurrentRectangle.Id).Cuts;
                    var SplitHeight = PreviousSplitHeight(page, CurrentRectangle.Id);
                    var ValidCuts = Cuts.Where(x => Y + x - SplitHeight <= setting.Height);
                    if (!ValidCuts.Any())
                    {
                        var NextColumn = CurrentColumn.SubList(i);
                        NextColumn.AddRange(page.Columns[columnIndex + 1]);
                        page.Columns[columnIndex + 1] = NextColumn;
                        page.Columns[columnIndex] = CurrentColumn.SubList(0, i);
                    }
                    else
                    {
                        var MaxCut = ValidCuts.Max() - SplitHeight;
                        CurrentColumn[i] = new InOutRect
                        {
                            Id = CurrentRectangle.Id,
                            Rectangle = new Rectangle
                            {
                                Width = CurrentRectangle.Rectangle.Width,
                                Height = MaxCut,
                                X = CurrentRectangle.Rectangle.X,
                                Y = Y,
                            }
                        };

                        var X = CurrentColumn.Max(x => x.Rectangle.X + x.Rectangle.Width) + setting.Spacing;
                        var NextColumn = new List<InOutRect> {
                            new InOutRect
                            {
                                Id = CurrentRectangle.Id,
                                Rectangle = new Rectangle
                                {
                                    Width = CurrentRectangle.Rectangle.Width,
                                    Height = CurrentRectangle.Rectangle.Height - MaxCut,
                                    X = X,
                                    Y = 0,
                                }
                            }
                        };

                        NextColumn.AddRange(CurrentColumn.SubList(i + 1));
                        NextColumn.AddRange(page.Columns[columnIndex + 1]);
                        page.Columns[columnIndex + 1] = NextColumn;
                        page.Columns[columnIndex] = CurrentColumn.SubList(0, i + 1);
                    }

                    UpdateYCoordinates(page, setting, columnIndex + 1);
                    break;
                }
            }
        }

        private static void Main(string[] args)
        {
            if (args.Length != 6)
            {
                Console.WriteLine("RectangleCompression \"path to input file\" \"path to output file\" width height spacing padding\r\nE.g. RectangleCompression \"c:\\input.txt\" \"c:\\output.txt\" 700 1000 10 5");
                return;
            }

            var Setting = new PageSetting { Width = int.Parse(args[2]), Height = int.Parse(args[3]), Spacing = int.Parse(args[4]), Padding = int.Parse(args[5]) };
            var InputPath = args[0];
            var OutputPath = args[1];

            // this would be replaced by whatever mechanism supplies the cut values
            DemoCutDict = CutoffsFromFile(InputPath);
            var RectangeList = RectanglesFromFile(InputPath);

            var LastId = RectangeList.Last().Id;

            if (!IsHorizontallyValid(RectangeList, Setting.Width))
                throw new Exception("A rectangle does not fit horizonally in the dimensions provided.");

            var Result = new List<Page>();
            var Pages = MaxRectanglesOnPage(RectangeList, Setting);
            Result.Add(Compress(Pages[0], Setting));
            var PageIndex = Pages[0].Columns.Last().Last().Id;

            while (Pages.Count > 1 || PageIndex != LastId)
            {
                var NewRectangles = Pages.Count > 1 ? Pages.Skip(1).Select(x => x.Columns)
                    .SelectMany(x => x).SelectMany(x => x).ToList() : new List<InOutRect>();
                NewRectangles.AddRange(RectangeList.SubList(PageIndex));
                Pages = MaxRectanglesOnPage(NewRectangles, Setting);
                Result.Add(Compress(Pages[0], Setting));
                PageIndex = Pages[0].Columns.Last().Last().Id;
            }

            PagesToOutputFile(Result, OutputPath);
        }

        public class InOutRect
        {
            public int Id { get; set; }

            public Rectangle Rectangle { get; set; }
        }

        public class Page
        {
            public List<List<InOutRect>> Columns { get; set; }
        }

        public class PageSetting
        {
            public int Height { get; set; }

            public int Padding { get; set; }

            public int Spacing { get; set; }

            public int Width { get; set; }
        }

        public class PlacementTree
        {
            public PlacementTree()
            {
                Placements = new List<Placement>();
                Pages = new List<Page> { new Page { Columns = new List<List<InOutRect>>() } };
            }

            public PlacementTree(List<Placement> placements, List<Page> pages)
            {
                Placements = placements;
                Pages = pages;
            }

            public PlacementTree Adjacent { get; set; }

            public List<Page> Pages { get; set; }

            public List<Placement> Placements { get; set; }

            public PlacementTree Under { get; set; }
        }

        public class RectangleCut
        {
            public List<int> Cuts { get; set; }
        }
    }
}