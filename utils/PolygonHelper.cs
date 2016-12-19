/*
This file is part of MatterSlice. A commandline utility for
generating 3D printing GCode.

Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using System.IO;
using MSClipperLib;

namespace MatterHackers.MatterSlice
{
	using System;

	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	public static class PolygonHelper
	{
		public static double Area(this Polygon polygon)
		{
			return Clipper.Area(polygon);
		}

		public static IntPoint back(this Polygon polygon)
		{
			return polygon[polygon.Count - 1];
		}

		public static bool CalcIntersection(IntPoint a1, IntPoint a2,
											  IntPoint b1, IntPoint b2,
											  out IntPoint position)
		{
			position = new IntPoint();

			long intersection_epsilon = 1;
			long num = (a1.Y - b1.Y) * (b2.X - b1.X) - (a1.X - b1.X) * (b2.Y - b1.Y);
			long den = (a2.X - a1.X) * (b2.Y - b1.Y) - (a2.Y - a1.Y) * (b2.X - b1.X);
			if (Math.Abs(den) < intersection_epsilon)
			{
				return false;
			}

			position.X = a1.X + (a2.X - a1.X) * num / den;
			position.Y = a1.Y + (a2.Y - a1.Y) * num / den;

			return true;
		}

		public static IntPoint CenterOfMass(this Polygon polygon)
		{
			IntPoint center = new IntPoint();
			for (int positionIndex = 0; positionIndex < polygon.Count; positionIndex++)
			{
				center += polygon[positionIndex];
			}

			center /= polygon.Count;
			return center;
		}

		public static Polygon CreateConvexHull(this Polygon inPolygon)
		{
			return new Polygon(GrahamScan.GetConvexHull(inPolygon));
		}

		public static Polygon CreateFromString(string polygonString)
		{
			Polygon output = new Polygon();
			string[] intPointData = polygonString.Split(',');
			for (int i = 0; i < intPointData.Length - 1; i += 2)
			{
				string elementX = intPointData[i];
				string elementY = intPointData[i + 1];
				IntPoint nextIntPoint = new IntPoint(int.Parse(elementX.Substring(2)), int.Parse(elementY.Substring(3)));
				output.Add(nextIntPoint);
			}

			return output;
		}

		public static bool DescribesSameShape(this Polygon a, Polygon b)
		{
			if (a.Count != b.Count)
			{
				return false;
			}

			// find first same point
			for (int indexB = 0; indexB < b.Count; indexB++)
			{
				if (a[0] == b[indexB])
				{
					// check if any point are different
					for (int indexA = 1; indexA < a.Count; indexA++)
					{
						if (a[indexA] != b[(indexB + indexA) % b.Count])
						{
							return false;
						}
					}

					// they are all the same
					return true;
				}
			}

			return false;
		}

		public static void ExpandToInclude(this IntRect inRect, IntRect otherRect)
		{
			if (otherRect.left < inRect.left) inRect.left = otherRect.left;
			if (otherRect.top < inRect.top) inRect.top = otherRect.top;
			if (otherRect.right > inRect.right) inRect.right = otherRect.right;
			if (otherRect.bottom > inRect.bottom) inRect.bottom = otherRect.bottom;
		}

		public static void FindCrossingPoints(this Polygon polygon, IntPoint start, IntPoint end, List<Tuple<int, IntPoint>> crossings)
		{
			crossings.Clear();
			IntPoint segmentDelta = end - start;
			IntPoint normal = segmentDelta.Normal(1000);
			IntPoint edgeStart = polygon[polygon.Count - 1];
			for (int i = 0; i < polygon.Count; i++)
			{
				IntPoint edgeEnd = polygon[i];
				IntPoint intersection;
				if (CalcIntersection(start, end, edgeStart, edgeEnd, out intersection)
					&& PointWithinStartEnd(edgeStart, edgeEnd, intersection))
				{
					IntPoint pointRelStart = intersection - start;
					long distanceFromStart = normal.Dot(pointRelStart) / 1000;
					if (distanceFromStart >= 0 && distanceFromStart <= segmentDelta.Length())
					{
						crossings.Add(new Tuple<int, IntPoint>(i, intersection));
					}
				}

				edgeStart = edgeEnd;
			}
		}

		public static int FindTouchingEdge(this Polygon polygon, IntPoint position, long maxDistance = 0)
		{
			throw new NotImplementedException();
		}

		public static IntPoint getBoundaryPointWithOffset(Polygon poly, int point_idx, long offset)
		{
			IntPoint p0 = poly[(point_idx > 0) ? (point_idx - 1) : (poly.size() - 1)];
			IntPoint p1 = poly[point_idx];
			IntPoint p2 = poly[(point_idx < (poly.size() - 1)) ? (point_idx + 1) : 0];

			IntPoint off0 = ((p1 - p0).Normal(1000)).CrossZ(); // 1.0 for some precision
			IntPoint off1 = ((p2 - p1).Normal(1000)).CrossZ(); // 1.0 for some precision
			IntPoint n = (off0 + off1).Normal(-offset);

			return p1 + n;
		}

		public static IntRect GetBounds(this Polygon inPolygon)
		{
			if (inPolygon.Count == 0)
			{
				return new IntRect(0, 0, 0, 0);
			}

			IntRect result = new IntRect();
			result.left = inPolygon[0].X;
			result.right = result.left;
			result.top = inPolygon[0].Y;
			result.bottom = result.top;
			for (int pointIndex = 1; pointIndex < inPolygon.Count; pointIndex++)
			{
				if (inPolygon[pointIndex].X < result.left)
				{
					result.left = inPolygon[pointIndex].X;
				}
				else if (inPolygon[pointIndex].X > result.right)
				{
					result.right = inPolygon[pointIndex].X;
				}

				if (inPolygon[pointIndex].Y < result.top)
				{
					result.top = inPolygon[pointIndex].Y;
				}
				else if (inPolygon[pointIndex].Y > result.bottom)
				{
					result.bottom = inPolygon[pointIndex].Y;
				}
			}

			return result;
		}

		public static bool Inside(this Polygon polygon, IntPoint testPoint)
		{
			int positionOnPolygon = Clipper.PointInPolygon(testPoint, polygon);
			if (positionOnPolygon == 0) // not inside or on boundary
			{
				return false;
			}

			return true;
		}

		public static bool LineSegementsIntersect(IntPoint p, IntPoint p2, IntPoint q, IntPoint q2,
					out IntPoint intersection)
		{
			intersection = new IntPoint();

			var r = p2 - p;
			var s = q2 - q;
			var rxs = r.CrossXy(s);
			var qpxr = (q - p).CrossXy(r);

			// If r x s = 0 and (q - p) x r = 0, then the two lines are collinear.
			if (rxs == 0 && qpxr == 0)
			{
				// 1. If either  0 <= (q - p) * r <= r * r or 0 <= (p - q) * s <= * s
				// then the two lines are overlapping,

				// 2. If neither 0 <= (q - p) * r = r * r nor 0 <= (p - q) * s <= s * s
				// then the two lines are collinear but disjoint.
				// No need to implement this expression, as it follows from the expression above.
				return false;
			}

			// 3. If r x s = 0 and (q - p) x r != 0, then the two lines are parallel and non-intersecting.
			if (rxs == 0 && qpxr != 0)
			{
				return false;
			}

			// t = (q - p) x s / (r x s)
			var t = (q - p).CrossXy(s);
			var u = (q - p).CrossXy(r);

			// 4. If r x s != 0 and 0 <= t <= 1 and 0 <= u <= 1
			// the two line segments meet at the point p + t r = q + u s.
			if (rxs != 0 && (t > 0 && t <= rxs) && (u > 0 && u <= rxs))
			{
				// We can calculate the intersection point using either t or u.
				intersection = p + r * t / rxs;

				// An intersection was found.
				return true;
			}

			// 5. Otherwise, the two line segments are not parallel but do not intersect.
			return false;
		}

		public static long MinX(this Polygon polygon)
		{
			long minX = long.MaxValue;
			foreach (var point in polygon)
			{
				if (point.X < minX)
				{
					minX = point.X;
				}
			}

			return minX;
		}

		public static void OptimizePolygon(this Polygon polygon)
		{
			IntPoint previousPoint = polygon[polygon.Count - 1];
			for (int i = 0; i < polygon.Count; i++)
			{
				IntPoint currentPoint = polygon[i];
				if ((previousPoint - currentPoint).IsShorterThen(10))
				{
					polygon.RemoveAt(i);
					i--;
				}
				else
				{
					IntPoint nextPoint;
					if (i < polygon.Count - 1)
					{
						nextPoint = polygon[i + 1];
					}
					else
					{
						nextPoint = polygon[0];
					}

					IntPoint diff0 = (currentPoint - previousPoint).SetLength(1000000);
					IntPoint diff2 = (currentPoint - nextPoint).SetLength(1000000);

					long d = diff0.Dot(diff2);
					if (d < -999999000000)
					{
						polygon.RemoveAt(i);
						i--;
					}
					else
					{
						previousPoint = currentPoint;
					}
				}
			}
		}

		public static bool Orientation(this Polygon polygon)
		{
			return Clipper.Orientation(polygon);
		}

		//returns 0 if false, +1 if true, -1 if pt ON polygon boundary
		public static int PointIsInside(this Polygon polygon, IntPoint testPoint)
		{
			return Clipper.PointInPolygon(testPoint, polygon);
		}

		public static bool PointWithinStartEnd(IntPoint start, IntPoint end, IntPoint testPosition)
		{
			IntPoint segmentDelta = end - start;
			IntPoint normal = segmentDelta.Normal(1000);
			IntPoint pointRelStart = testPosition - start;
			long distanceFromStart = normal.Dot(pointRelStart) / 1000;

			if (distanceFromStart >= 0 && distanceFromStart <= segmentDelta.Length())
			{
				return true;
			}

			return false;
		}

		public static bool polygonCollidesWithlineSegment(Polygon poly, IntPoint startPoint, IntPoint endPoint)
		{
			IntPoint diff = endPoint - startPoint;

			PointMatrix transformation_matrix = new PointMatrix(diff);
			IntPoint transformed_startPoint = transformation_matrix.apply(startPoint);
			IntPoint transformed_endPoint = transformation_matrix.apply(endPoint);

			return polygonCollidesWithlineSegment(poly, transformed_startPoint, transformed_endPoint, transformation_matrix);
		}

		public static bool polygonCollidesWithlineSegment(Polygon poly, IntPoint transformed_startPoint, IntPoint transformed_endPoint, PointMatrix transformation_matrix)
		{
			IntPoint p0 = transformation_matrix.apply(poly.back());
			foreach (IntPoint p1_ in poly)
			{
				IntPoint p1 = transformation_matrix.apply(p1_);
				if ((p0.Y >= transformed_startPoint.Y && p1.Y <= transformed_startPoint.Y) || (p1.Y >= transformed_startPoint.Y && p0.Y <= transformed_startPoint.Y))
				{
					long x;
					if (p1.Y == p0.Y)
					{
						x = p0.X;
					}
					else
					{
						x = p0.X + (p1.X - p0.X) * (transformed_startPoint.Y - p0.Y) / (p1.Y - p0.Y);
					}

					if (x >= transformed_startPoint.X && x <= transformed_endPoint.X)
						return true;
				}
				p0 = p1;
			}
			return false;
		}

		public static long PolygonLength(this Polygon polygon, bool areClosed = true)
		{
			long length = 0;
			if (polygon.Count > 1)
			{
				IntPoint previousPoint = polygon[0];
				if (areClosed)
				{
					previousPoint = polygon[polygon.Count - 1];
				}
				for (int i = areClosed ? 0 : 1; i < polygon.Count; i++)
				{
					IntPoint currentPoint = polygon[i];
					length += (previousPoint - currentPoint).Length();
					previousPoint = currentPoint;
				}
			}

			return length;
		}

		public static void Reverse(this Polygon polygon)
		{
			polygon.Reverse();
		}

		public static long GetShortestDistanceAround(this Polygon polygon, int startEdgeIndex, IntPoint startPosition, int endEdgeIndex, IntPoint endPosition)
		{
			if (polygon.Count > 2)
			{
				int lastPositiveIndex = startEdgeIndex;
				// Get distance to start point
				long positiveDistance = (polygon[(startEdgeIndex+1)% polygon.Count] - startPosition).Length();
				long totalDistance = polygon.PolygonLength();
				bool first = true;
				for (int i = 0; i < polygon.Count; i++)
				{
					int positiveIndex = (lastPositiveIndex + 1) % polygon.Count;
					if (!first)
					{
						positiveDistance += (polygon[positiveIndex] - polygon[lastPositiveIndex]).Length();
					}
					if (lastPositiveIndex == endEdgeIndex)
					{
						positiveDistance -= (polygon[positiveIndex] - endPosition).Length();
						if(positiveDistance < 0)
						{
							return positiveDistance;
						}
						break;
					}

					first = false;

					lastPositiveIndex = positiveIndex;
				}

				if(positiveDistance < totalDistance/2)
				{
					return positiveDistance;
				}

				return -(totalDistance - positiveDistance);
			}

			return 0;
		}

		public static void SaveToGCode(this Polygon polygon, string filename)
		{
			double scale = 1000;
			StreamWriter stream = new StreamWriter(filename);
			stream.Write("; some gcode to look at the layer segments\n");
			int extrudeAmount = 0;
			double firstX = 0;
			double firstY = 0;
			for (int intPointIndex = 0; intPointIndex < polygon.Count; intPointIndex++)
			{
				double x = (double)(polygon[intPointIndex].X) / scale;
				double y = (double)(polygon[intPointIndex].Y) / scale;
				if (intPointIndex == 0)
				{
					firstX = x;
					firstY = y;
					stream.Write("G1 X{0} Y{1}\n", x, y);
				}
				else
				{
					stream.Write("G1 X{0} Y{1} E{2}\n", x, y, ++extrudeAmount);
				}
			}
			stream.Write("G1 X{0} Y{1} E{2}\n", firstX, firstY, ++extrudeAmount);

			stream.Close();
		}

		public static int size(this Polygon polygon)
		{
			return polygon.Count;
		}

		public static string WriteToString(this Polygon polygon)
		{
			string total = "";
			foreach (IntPoint point in polygon)
			{
				total += point.ToString() + ",";
			}
			return total;
		}

		// true if p0 -> p1 -> p2 is strictly convex.
		private static bool convex3(long x0, long y0, long x1, long y1, long x2, long y2)
		{
			return (y1 - y0) * (x1 - x2) > (x0 - x1) * (y2 - y1);
		}

		private static bool convex3(IntPoint p0, IntPoint p1, IntPoint p2)
		{
			return convex3(p0.X, p0.Y, p1.X, p1.Y, p2.X, p2.Y);
		}

		public class DirectionSorter : IComparer<Tuple<int, IntPoint>>
		{
			private IntPoint direction;
			private IntPoint start;

			public DirectionSorter(IntPoint start, IntPoint end)
			{
				this.start = start;
				this.direction = (end - start).Normal(1000);
			}

			public int Compare(Tuple<int, IntPoint> a, Tuple<int, IntPoint> b)
			{
				long distToA = direction.Dot(a.Item2 - start) / 1000;
				long distToB = direction.Dot(b.Item2 - start) / 1000;

				return distToA.CompareTo(distToB);
			}
		}

		// operator to sort IntPoint by y
		// (and then by X, where Y are equal)
		public class IntPointSorterYX : IComparer<IntPoint>
		{
			public virtual int Compare(IntPoint a, IntPoint b)
			{
				if (a.Y == b.Y)
				{
					return a.X.CompareTo(b.X);
				}
				else
				{
					return a.Y.CompareTo(b.Y);
				}
			}
		}
	}

	// Axis aligned boundary box
	public class Aabb
	{
		public IntPoint min, max;

		public Aabb()
		{
			min = new IntPoint(long.MinValue, long.MinValue);
			max = new IntPoint(long.MinValue, long.MinValue);
		}

		public Aabb(Polygons polys)
		{
			min = new IntPoint(long.MinValue, long.MinValue);
			max = new IntPoint(long.MinValue, long.MinValue);
			Calculate(polys);
		}

		public void Calculate(Polygons polys)
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

		public bool Hit(Aabb other)
		{
			if (max.X < other.min.X) return false;
			if (min.X > other.max.X) return false;
			if (max.Y < other.min.Y) return false;
			if (min.Y > other.max.Y) return false;
			return true;
		}
	}
}