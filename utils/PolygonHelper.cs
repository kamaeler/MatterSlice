/*
Copyright (c) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MatterSlice is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MatterSlice.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Path = List<IntPoint>;
    using Paths = List<List<IntPoint>>;
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    static class PolygonHelper
    {
        public static bool Orientation(this Polygon polygon)
        {
            return Clipper.Orientation(polygon);
        }

        public static void Reverse(this Polygon polygon)
        {
            polygon.Reverse();
        }

        public static long PolygonLength(this Polygon polygon)
        {
            long length = 0;
            IntPoint p0 = polygon[polygon.Count - 1];
            for (int n = 0; n < polygon.Count; n++)
            {
                IntPoint p1 = polygon[n];
                length += (p0 - p1).Length();
                p0 = p1;
            }
            return length;
        }

        public static double Area(this Polygon polygon)
        {
            return Clipper.Area(polygon);
        }

        public static void optimizePolygon(this Polygon polygon)
        {
            IntPoint p0 = polygon[polygon.Count - 1];
            for (int i = 0; i < polygon.Count; i++)
            {
                IntPoint p1 = polygon[i];
                if ((p0 - p1).IsShorterThen(10))
                {
                    polygon.RemoveAt(i);
                    i--;
                }
                else
                {
                    IntPoint p2;
                    if (i < polygon.Count - 1)
                    {
                        p2 = polygon[i + 1];
                    }
                    else
                    {
                        p2 = polygon[0];
                    }

                    IntPoint diff0 = (p1 - p0).SetLength(1000000);
                    IntPoint diff2 = (p1 - p2).SetLength(1000000);

                    long d = diff0.Dot(diff2);
                    if (d < -999999000000)
                    {
                        polygon.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        p0 = p1;
                    }
                }
            }
        }

        public static IntPoint CenterOfMass(this Polygon polygon)
        {
            double x = 0, y = 0;
            IntPoint p0 = (polygon)[polygon.Count - 1];
            for (int n = 0; n < polygon.Count; n++)
            {
                IntPoint p1 = (polygon)[n];
                double second_factor = (p0.X * p1.Y) - (p1.X * p0.Y);

                x += (double)(p0.X + p1.X) * second_factor;
                y += (double)(p0.Y + p1.Y) * second_factor;
                p0 = p1;
            }

            double area = Clipper.Area(polygon);
            x = x / 6 / area;
            y = y / 6 / area;

            if (x < 0)
            {
                x = -x;
                y = -y;
            }
            return new IntPoint(x, y);
        }
    }

    static class PolygonsHelper
    {
        public static void AddAll(this Polygons polygons, Polygons other)
        {
            for (int n = 0; n < other.Count; n++)
            {
                polygons.Add(other[n]);
            }
        }

        public static Polygons CreateDifference(this Polygons polygons, Polygons other)
        {
            Polygons ret = new Polygons();
            Clipper clipper = new Clipper();
            clipper.AddPaths(polygons, PolyType.ptSubject, true);
            clipper.AddPaths(other, PolyType.ptClip, true);
            clipper.Execute(ClipType.ctDifference, ret);
            return ret;
        }

        public static Polygons CreateUnion(this Polygons polygons, Polygons other)
        {
            Polygons ret = new Polygons();
            Clipper clipper = new Clipper();
            clipper.AddPaths(polygons, PolyType.ptSubject, true);
            clipper.AddPaths(other, PolyType.ptSubject, true);
            clipper.Execute(ClipType.ctUnion, ret, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            return ret;
        }

        public static Polygons CreateIntersection(this Polygons polygons, Polygons other)
        {
            Polygons ret = new Polygons();
            Clipper clipper = new Clipper();
            clipper.AddPaths(polygons, PolyType.ptSubject, true);
            clipper.AddPaths(other, PolyType.ptClip, true);
            clipper.Execute(ClipType.ctIntersection, ret);
            return ret;
        }

        public static void optimizePolygons(this Polygons polygons)
        {
            for (int n = 0; n < polygons.Count; n++)
            {
                polygons[n].optimizePolygon();
                if (polygons[n].Count < 3)
                {
                    polygons.RemoveAt(n);
                    n--;
                }
            }
        }
        
        public static Polygons Offset(this Polygons polygons, int distance)
        {
            return Clipper.OffsetPaths(polygons, distance, JoinType.jtMiter, EndType_.etClosed, 2.0);
        }

        public static List<Polygons> SplitIntoParts(this Polygons polygons, bool unionAll = false)
        {
            List<Polygons> ret = new List<Polygons>();
            Clipper clipper = new Clipper();
            PolyTree resultPolyTree = new PolyTree();
            clipper.AddPaths(polygons, PolyType.ptSubject, true);
            if (unionAll)
            {
                clipper.Execute(ClipType.ctUnion, resultPolyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            }
            else
            {
                clipper.Execute(ClipType.ctUnion, resultPolyTree);
            }

            polygons._processPolyTreeNode(resultPolyTree, ret);
            return ret;
        }

        static void _processPolyTreeNode(this Polygons polygonsIn, PolyNode node, List<Polygons> ret)
        {
            for (int n = 0; n < node.ChildCount; n++)
            {
                PolyNode child = node.Childs[n];
                Polygons polygons = new Polygons();
                polygons.Add(child.Contour);
                for (int i = 0; i < child.ChildCount; i++)
                {
                    polygons.Add(child.Childs[i].Contour);
                    polygonsIn._processPolyTreeNode(child.Childs[i], ret);
                }
                ret.Add(polygons);
            }
        }

        public static Polygons processEvenOdd(this Polygons polygons)
        {
            Polygons ret = new Polygons();
            Clipper clipper = new Clipper();
            clipper.AddPaths(polygons, PolyType.ptSubject, true);
            clipper.Execute(ClipType.ctUnion, ret);
            return ret;
        }

        public static long polygonLength(this Polygons polygons)
        {
            long length = 0;
            for (int i = 0; i < polygons.Count; i++)
            {
                IntPoint p0 = polygons[i][polygons[i].Count - 1];
                for (int n = 0; n < polygons[i].Count; n++)
                {
                    IntPoint p1 = polygons[i][n];
                    length += (p0 - p1).vSize();
                    p0 = p1;
                }
            }
            return length;
        }

        public static void applyMatrix(this Polygons polygons, PointMatrix matrix)
        {
            for (int i = 0; i < polygons.Count; i++)
            {
                for (int j = 0; j < polygons[i].Count; j++)
                {
                    polygons[i][j] = matrix.apply(polygons[i][j]);
                }
            }
        }
    }

    // Axis aligned boundary box
    public class AABB
    {
        public IntPoint min, max;

        public AABB()
        {
            min = new IntPoint(long.MinValue, long.MinValue);
            max = new IntPoint(long.MinValue, long.MinValue);
        }

        public AABB(Polygons polys)
        {
            min = new IntPoint(long.MinValue, long.MinValue);
            max = new IntPoint(long.MinValue, long.MinValue);
            calculate(polys);
        }

        public void calculate(Polygons polys)
        {
            min = new IntPoint(long.MaxValue, long.MaxValue);
            max = new IntPoint(long.MinValue, long.MinValue);
            for (int i = 0; i < polys.Count; i++)
            {
                for (int j = 0; j < polys[i].Count; j++)
                {
                    if (min.X > polys[i][j].X) min.X = polys[i][j].X;
                    if (min.Y > polys[i][j].Y) min.Y = polys[i][j].Y;
                    if (max.X < polys[i][j].X) max.X = polys[i][j].X;
                    if (max.Y < polys[i][j].Y) max.Y = polys[i][j].Y;
                }
            }
        }

        public bool hit(AABB other)
        {
            if (max.X < other.min.X) return false;
            if (min.X > other.max.X) return false;
            if (max.Y < other.min.Y) return false;
            if (min.Y > other.max.Y) return false;
            return true;
        }
    }
}