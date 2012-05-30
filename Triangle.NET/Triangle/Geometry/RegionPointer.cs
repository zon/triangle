﻿// -----------------------------------------------------------------------
// <copyright file="RegionPointer.cs" company="">
// Original Triangle code by Jonathan Richard Shewchuk, http://www.cs.cmu.edu/~quake/triangle.html
// Triangle.NET code by Christian Woltering, http://home.edo.tu-dortmund.de/~woltering/triangle/
// </copyright>
// -----------------------------------------------------------------------

namespace TriangleNet.Geometry
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Pointer to a region in the mesh geometry. A region is a well-defined
    /// subset of the geomerty (enclosed by subsegments).
    /// </summary>
    public class RegionPointer
    {
        internal Point point;
        internal double area;
        internal double attribute;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegionPointer" /> class.
        /// </summary>
        /// <param name="x">X coordinate of the region.</param>
        /// <param name="y">Y coordinate of the region.</param>
        /// <param name="area">Area constraint.</param>
        /// <param name="attribute">Region attribute.</param>
        public RegionPointer(double x, double y, double area, double attribute)
        {
            this.point = new Point(x, y);
            this.area = area;
            this.attribute = attribute;
        }

        /// <summary>
        /// Gets the location of the region.
        /// </summary>
        internal Point Point
        {
            get { return point; }
        }

        /// <summary>
        /// Gets the area constraint.
        /// </summary>
        internal double Area
        {
            get { return area; }
        }

        /// <summary>
        /// Gets the region attribute.
        /// </summary>
        internal double Attribute
        {
            get { return attribute; }
        }
    }
}
