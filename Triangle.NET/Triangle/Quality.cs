﻿// -----------------------------------------------------------------------
// <copyright file="Quality.cs">
// Original Triangle code by Jonathan Richard Shewchuk, http://www.cs.cmu.edu/~quake/triangle.html
// Triangle.NET code by Christian Woltering, http://home.edo.tu-dortmund.de/~woltering/triangle/
// </copyright>
// -----------------------------------------------------------------------

namespace TriangleNet
{
    using System;
    using System.Collections.Generic;
    using TriangleNet.Data;
    using TriangleNet.Log;

    /// <summary>
    /// Provides methods for mesh quality enforcement and testing.
    /// </summary>
    class Quality
    {
        Queue<BadSubseg> badsubsegs;
        BadTriQueue queue;
        Mesh mesh;
        Func<Vertex, Vertex, Vertex, double, bool> userTest;

        ILog<string> logger;

        public Quality(Mesh m)
        {
            logger = SimpleLogger.Instance;

            badsubsegs = new Queue<BadSubseg>();
            queue = new BadTriQueue();
            mesh = m;
        }

        /// <summary>
        /// Deallocate space for a bad subsegment, marking it dead.
        /// </summary>
        /// <param name="dyingseg"></param>
        public void AddBadSubseg(BadSubseg badseg)
        {
            badsubsegs.Enqueue(badseg);
        }

        #region Check

        /// <summary>
        /// Test the mesh for topological consistency.
        /// </summary>
        public bool CheckMesh()
        {
            Otri tri = default(Otri);
            Otri oppotri = default(Otri), oppooppotri = default(Otri);
            Vertex triorg, tridest, triapex;
            Vertex oppoorg, oppodest;
            int horrors;
            bool saveexact;

            // Temporarily turn on exact arithmetic if it's off.
            saveexact = Behavior.NoExact;
            Behavior.NoExact = false;
            horrors = 0;

            // Run through the list of triangles, checking each one.
            foreach (var t in mesh.triangles.Values)
            {
                tri.triangle = t;

                // Check all three edges of the triangle.
                for (tri.orient = 0; tri.orient < 3; tri.orient++)
                {
                    triorg = tri.Org();
                    tridest = tri.Dest();
                    if (tri.orient == 0)
                    {   // Only test for inversion once.
                        // Test if the triangle is flat or inverted.
                        triapex = tri.Apex();
                        if (Primitives.CounterClockwise(triorg.pt, tridest.pt, triapex.pt) <= 0.0)
                        {
                            horrors++;
                        }
                    }
                    // Find the neighboring triangle on this edge.
                    tri.Sym(ref oppotri);
                    if (oppotri.triangle != Mesh.dummytri)
                    {
                        // Check that the triangle's neighbor knows it's a neighbor.
                        oppotri.Sym(ref oppooppotri);
                        if ((tri.triangle != oppooppotri.triangle) || (tri.orient != oppooppotri.orient))
                        {
                            if (tri.triangle == oppooppotri.triangle)
                            {
                                logger.Warning("Asymmetric triangle-triangle bond: (Right triangle, wrong orientation)",
                                    "Quality.CheckMesh()");
                            }

                            horrors++;
                        }
                        // Check that both triangles agree on the identities
                        // of their shared vertices.
                        oppoorg = oppotri.Org();
                        oppodest = oppotri.Dest();
                        if ((triorg != oppodest) || (tridest != oppoorg))
                        {
                            logger.Warning("Mismatched edge coordinates between two triangles.",
                                "Quality.CheckMesh()");

                            horrors++;
                        }
                    }
                }
            }

            if (horrors == 0)
            {
                if (Behavior.Verbose)
                {
                    logger.Info("In my studied opinion, the mesh appears to be consistent.");
                }
            }
            else if (horrors == 1)
            {
                logger.Info("Precisely one festering wound discovered.");
            }
            else
            {
                logger.Info(horrors + " abominations witnessed.");
            }
            // Restore the status of exact arithmetic.
            Behavior.NoExact = saveexact;

            return (horrors == 0);
        }

        /// <summary>
        /// Ensure that the mesh is (constrained) Delaunay.
        /// </summary>
        public bool CheckDelaunay()
        {
            Otri triangleloop = default(Otri);
            Otri oppotri = default(Otri);
            Osub opposubseg = default(Osub);
            Vertex triorg, tridest, triapex;
            Vertex oppoapex;
            bool shouldbedelaunay;
            int horrors;
            bool saveexact;

            // Temporarily turn on exact arithmetic if it's off.
            saveexact = Behavior.NoExact;
            Behavior.NoExact = false;
            horrors = 0;

            // Run through the list of triangles, checking each one.
            foreach (var t in mesh.triangles.Values)
            {
                triangleloop.triangle = t;

                // Check all three edges of the triangle.
                for (triangleloop.orient = 0; triangleloop.orient < 3;
                     triangleloop.orient++)
                {
                    triorg = triangleloop.Org();
                    tridest = triangleloop.Dest();
                    triapex = triangleloop.Apex();
                    triangleloop.Sym(ref oppotri);
                    oppoapex = oppotri.Apex();
                    // Only test that the edge is locally Delaunay if there is an
                    // adjoining triangle whose pointer is larger (to ensure that
                    // each pair isn't tested twice).
                    shouldbedelaunay = (oppotri.triangle != Mesh.dummytri) &&
                          !Otri.IsDead(oppotri.triangle) && //(triangleloop.tri < oppotri.tri) &&
                          (triorg != mesh.infvertex1) && (triorg != mesh.infvertex2) &&
                          (triorg != mesh.infvertex3) &&
                          (tridest != mesh.infvertex1) && (tridest != mesh.infvertex2) &&
                          (tridest != mesh.infvertex3) &&
                          (triapex != mesh.infvertex1) && (triapex != mesh.infvertex2) &&
                          (triapex != mesh.infvertex3) &&
                          (oppoapex != mesh.infvertex1) && (oppoapex != mesh.infvertex2) &&
                          (oppoapex != mesh.infvertex3);
                    if (mesh.checksegments && shouldbedelaunay)
                    {
                        // If a subsegment separates the triangles, then the edge is
                        // constrained, so no local Delaunay test should be done.
                        triangleloop.SegPivot(ref opposubseg);
                        if (opposubseg.ss != Mesh.dummysub)
                        {
                            shouldbedelaunay = false;
                        }
                    }
                    if (shouldbedelaunay)
                    {
                        if (Primitives.NonRegular(triorg.pt, tridest.pt, triapex.pt, oppoapex.pt) > 0.0)
                        {
                            logger.Warning("Non-regular pair of triangles found.", "Quality.CheckDelaunay()");
                            horrors++;
                        }
                    }
                }

            }
            if (horrors == 0)
            {
                if (Behavior.Verbose)
                {
                    logger.Info("By virtue of my perceptive intelligence, I declare the mesh Delaunay.");
                }
            }
            else if (horrors == 1)
            {
                logger.Info("Precisely one terrifying transgression identified.");
            }
            else
            {
                logger.Info(horrors + " obscenities viewed with horror.");
            }
            // Restore the status of exact arithmetic.
            Behavior.NoExact = saveexact;

            return (horrors == 0);
        }

        /// <summary>
        /// Check a subsegment to see if it is encroached; add it to the list if it is.
        /// </summary>
        /// <param name="testsubseg"></param>
        /// <returns>Returns a nonzero value if the subsegment is encroached.</returns>
        /// <remarks>
        /// A subsegment is encroached if there is a vertex in its diametral lens.
        /// For Ruppert's algorithm (-D switch), the "diametral lens" is the
        /// diametral circle. For Chew's algorithm (default), the diametral lens is
        /// just big enough to enclose two isosceles triangles whose bases are the
        /// subsegment. Each of the two isosceles triangles has two angles equal
        /// to 'b.minangle'.
        ///
        /// Chew's algorithm does not require diametral lenses at all--but they save
        /// time. Any vertex inside a subsegment's diametral lens implies that the
        /// triangle adjoining the subsegment will be too skinny, so it's only a
        /// matter of time before the encroaching vertex is deleted by Chew's
        /// algorithm. It's faster to simply not insert the doomed vertex in the
        /// first place, which is why I use diametral lenses with Chew's algorithm.
        /// </remarks>
        public int CheckSeg4Encroach(ref Osub testsubseg)
        {
            Otri neighbortri = default(Otri);
            Osub testsym = default(Osub);
            BadSubseg encroachedseg;
            double dotproduct;
            int encroached;
            int sides;
            Vertex eorg, edest, eapex;

            encroached = 0;
            sides = 0;

            eorg = testsubseg.Org();
            edest = testsubseg.Dest();
            // Check one neighbor of the subsegment.
            testsubseg.TriPivot(ref neighbortri);
            // Does the neighbor exist, or is this a boundary edge?
            if (neighbortri.triangle != Mesh.dummytri)
            {
                sides++;
                // Find a vertex opposite this subsegment.
                eapex = neighbortri.Apex();
                // Check whether the apex is in the diametral lens of the subsegment
                // (the diametral circle if 'conformdel' is set).  A dot product
                // of two sides of the triangle is used to check whether the angle
                // at the apex is greater than (180 - 2 'minangle') degrees (for
                // lenses; 90 degrees for diametral circles).
                dotproduct = (eorg.pt.X - eapex.pt.X) * (edest.pt.X - eapex.pt.X) +
                             (eorg.pt.Y - eapex.pt.Y) * (edest.pt.Y - eapex.pt.Y);
                if (dotproduct < 0.0)
                {
                    if (Behavior.ConformDel ||
                        (dotproduct * dotproduct >=
                         (2.0 * Behavior.GoodAngle - 1.0) * (2.0 * Behavior.GoodAngle - 1.0) *
                         ((eorg.pt.X - eapex.pt.X) * (eorg.pt.X - eapex.pt.X) +
                          (eorg.pt.Y - eapex.pt.Y) * (eorg.pt.Y - eapex.pt.Y)) *
                         ((edest.pt.X - eapex.pt.X) * (edest.pt.X - eapex.pt.X) +
                          (edest.pt.Y - eapex.pt.Y) * (edest.pt.Y - eapex.pt.Y))))
                    {
                        encroached = 1;
                    }
                }
            }
            // Check the other neighbor of the subsegment.
            testsubseg.Sym(ref testsym);
            testsym.TriPivot(ref neighbortri);
            // Does the neighbor exist, or is this a boundary edge?
            if (neighbortri.triangle != Mesh.dummytri)
            {
                sides++;
                // Find the other vertex opposite this subsegment.
                eapex = neighbortri.Apex();
                // Check whether the apex is in the diametral lens of the subsegment
                // (or the diametral circle, if 'conformdel' is set).
                dotproduct = (eorg.pt.X - eapex.pt.X) * (edest.pt.X - eapex.pt.X) +
                             (eorg.pt.Y - eapex.pt.Y) * (edest.pt.Y - eapex.pt.Y);
                if (dotproduct < 0.0)
                {
                    if (Behavior.ConformDel ||
                        (dotproduct * dotproduct >=
                         (2.0 * Behavior.GoodAngle - 1.0) * (2.0 * Behavior.GoodAngle - 1.0) *
                         ((eorg.pt.X - eapex.pt.X) * (eorg.pt.X - eapex.pt.X) +
                          (eorg.pt.Y - eapex.pt.Y) * (eorg.pt.Y - eapex.pt.Y)) *
                         ((edest.pt.X - eapex.pt.X) * (edest.pt.X - eapex.pt.X) +
                          (edest.pt.Y - eapex.pt.Y) * (edest.pt.Y - eapex.pt.Y))))
                    {
                        encroached += 2;
                    }
                }
            }

            if (encroached > 0 && (Behavior.NoBisect == 0 || ((Behavior.NoBisect == 1) && (sides == 2))))
            {
                // Add the subsegment to the list of encroached subsegments.
                // Be sure to get the orientation right.
                encroachedseg = new BadSubseg();
                if (encroached == 1)
                {
                    encroachedseg.encsubseg = testsubseg;
                    encroachedseg.subsegorg = eorg;
                    encroachedseg.subsegdest = edest;
                }
                else
                {
                    encroachedseg.encsubseg = testsym;
                    encroachedseg.subsegorg = edest;
                    encroachedseg.subsegdest = eorg;
                }
                
                badsubsegs.Enqueue(encroachedseg);
            }

            return encroached;
        }

        /// <summary>
        /// Test a triangle for quality and size.
        /// </summary>
        /// <param name="testtri">Triangle to check.</param>
        /// <remarks>
        /// Tests a triangle to see if it satisfies the minimum angle condition and
        /// the maximum area condition.  Triangles that aren't up to spec are added
        /// to the bad triangle queue.
        /// </remarks>
        public void TestTriangle(ref Otri testtri)
        {
            Otri tri1 = default(Otri), tri2 = default(Otri);
            Osub testsub = default(Osub);
            Vertex torg, tdest, tapex;
            Vertex base1, base2;
            Vertex org1, dest1, org2, dest2;
            Vertex joinvertex;
            double dxod, dyod, dxda, dyda, dxao, dyao;
            double dxod2, dyod2, dxda2, dyda2, dxao2, dyao2;
            double apexlen, orglen, destlen, minedge;
            double angle;
            double area;
            double dist1, dist2;

            double maxedge, maxangle;

            torg = testtri.Org();
            tdest = testtri.Dest();
            tapex = testtri.Apex();
            dxod = torg.pt.X - tdest.pt.X;
            dyod = torg.pt.Y - tdest.pt.Y;
            dxda = tdest.pt.X - tapex.pt.X;
            dyda = tdest.pt.Y - tapex.pt.Y;
            dxao = tapex.pt.X - torg.pt.X;
            dyao = tapex.pt.Y - torg.pt.Y;
            dxod2 = dxod * dxod;
            dyod2 = dyod * dyod;
            dxda2 = dxda * dxda;
            dyda2 = dyda * dyda;
            dxao2 = dxao * dxao;
            dyao2 = dyao * dyao;
            // Find the lengths of the triangle's three edges.
            apexlen = dxod2 + dyod2;
            orglen = dxda2 + dyda2;
            destlen = dxao2 + dyao2;

            if ((apexlen < orglen) && (apexlen < destlen))
            {
                // The edge opposite the apex is shortest.
                minedge = apexlen;
                // Find the square of the cosine of the angle at the apex.
                angle = dxda * dxao + dyda * dyao;
                angle = angle * angle / (orglen * destlen);
                base1 = torg;
                base2 = tdest;
                testtri.Copy(ref tri1);
            }
            else if (orglen < destlen)
            {
                // The edge opposite the origin is shortest.
                minedge = orglen;
                // Find the square of the cosine of the angle at the origin.
                angle = dxod * dxao + dyod * dyao;
                angle = angle * angle / (apexlen * destlen);
                base1 = tdest;
                base2 = tapex;
                testtri.Lnext(ref tri1);
            }
            else
            {
                // The edge opposite the destination is shortest.
                minedge = destlen;
                // Find the square of the cosine of the angle at the destination.
                angle = dxod * dxda + dyod * dyda;
                angle = angle * angle / (apexlen * orglen);
                base1 = tapex;
                base2 = torg;
                testtri.Lprev(ref tri1);
            }

            if (Behavior.VarArea || Behavior.FixedArea || Behavior.Usertest)
            {
                // Check whether the area is larger than permitted.
                area = 0.5 * (dxod * dyda - dyod * dxda);
                if (Behavior.FixedArea && (area > Behavior.MaxArea))
                {
                    // Add this triangle to the list of bad triangles.
                    queue.Enqueue(ref testtri, minedge, tapex, torg, tdest);
                    return;
                }

                // Nonpositive area constraints are treated as unconstrained.
                if ((Behavior.VarArea) && (area > testtri.triangle.area) && (testtri.triangle.area > 0.0))
                {
                    // Add this triangle to the list of bad triangles.
                    queue.Enqueue(ref testtri, minedge, tapex, torg, tdest);
                    return;
                }

                // Check whether the user thinks this triangle is too large.
                if (Behavior.Usertest && userTest != null)
                {
                    if (userTest(torg, tdest, tapex, area))
                    {
                        queue.Enqueue(ref testtri, minedge, tapex, torg, tdest);
                        return;
                    }
                }
            }

            // find the maximum edge and accordingly the pqr orientation
            if ((apexlen > orglen) && (apexlen > destlen))
            {
                // The edge opposite the apex is longest.
                maxedge = apexlen;
                // Find the cosine of the angle at the apex.
                maxangle = (orglen + destlen - apexlen) / (2 * Math.Sqrt(orglen) * Math.Sqrt(destlen));
            }
            else if (orglen > destlen)
            {
                // The edge opposite the origin is longest.
                maxedge = orglen;
                // Find the cosine of the angle at the origin.
                maxangle = (apexlen + destlen - orglen) / (2 * Math.Sqrt(apexlen) * Math.Sqrt(destlen));
            }
            else
            {
                // The edge opposite the destination is longest.
                maxedge = destlen;
                // Find the cosine of the angle at the destination.
                maxangle = (apexlen + orglen - destlen) / (2 * Math.Sqrt(apexlen) * Math.Sqrt(orglen));
            }

            // Check whether the angle is smaller than permitted.
            if ((angle > Behavior.GoodAngle) || (maxangle < Behavior.MaxGoodAngle && Behavior.MaxAngle != 0.0))
            {
                // Use the rules of Miller, Pav, and Walkington to decide that certain
                // triangles should not be split, even if they have bad angles.
                // A skinny triangle is not split if its shortest edge subtends a
                // small input angle, and both endpoints of the edge lie on a
                // concentric circular shell.  For convenience, I make a small
                // adjustment to that rule:  I check if the endpoints of the edge
                // both lie in segment interiors, equidistant from the apex where
                // the two segments meet.
                // First, check if both points lie in segment interiors.
                if ((base1.type == VertexType.SegmentVertex) &&
                    (base2.type == VertexType.SegmentVertex))
                {
                    // Check if both points lie in a common segment. If they do, the
                    // skinny triangle is enqueued to be split as usual.
                    tri1.SegPivot(ref testsub);
                    if (testsub.ss == Mesh.dummysub)
                    {
                        // No common segment.  Find a subsegment that contains 'torg'.
                        tri1.Copy(ref tri2);
                        do
                        {
                            tri1.OprevSelf();
                            tri1.SegPivot(ref testsub);
                        } while (testsub.ss == Mesh.dummysub);
                        // Find the endpoints of the containing segment.
                        org1 = testsub.SegOrg();
                        dest1 = testsub.SegDest();
                        // Find a subsegment that contains 'tdest'.
                        do
                        {
                            tri2.DnextSelf();
                            tri2.SegPivot(ref testsub);
                        } while (testsub.ss == Mesh.dummysub);
                        // Find the endpoints of the containing segment.
                        org2 = testsub.SegOrg();
                        dest2 = testsub.SegDest();
                        // Check if the two containing segments have an endpoint in common.
                        joinvertex = null;
                        if ((dest1.pt.X == org2.pt.X) && (dest1.pt.Y == org2.pt.Y))
                        {
                            joinvertex = dest1;
                        }
                        else if ((org1.pt.X == dest2.pt.X) && (org1.pt.Y == dest2.pt.Y))
                        {
                            joinvertex = org1;
                        }
                        if (joinvertex != null)
                        {
                            // Compute the distance from the common endpoint (of the two
                            // segments) to each of the endpoints of the shortest edge.
                            dist1 = ((base1.pt.X - joinvertex.pt.X) * (base1.pt.X - joinvertex.pt.X) +
                                     (base1.pt.Y - joinvertex.pt.Y) * (base1.pt.Y - joinvertex.pt.Y));
                            dist2 = ((base2.pt.X - joinvertex.pt.X) * (base2.pt.X - joinvertex.pt.X) +
                                     (base2.pt.Y - joinvertex.pt.Y) * (base2.pt.Y - joinvertex.pt.Y));
                            // If the two distances are equal, don't split the triangle.
                            if ((dist1 < 1.001 * dist2) && (dist1 > 0.999 * dist2))
                            {
                                // Return now to avoid enqueueing the bad triangle.
                                return;
                            }
                        }
                    }
                }

                // Add this triangle to the list of bad triangles.
                queue.Enqueue(ref testtri, minedge, tapex, torg, tdest);
            }
        }

        #endregion

        #region Maintanance

        /// <summary>
        /// Traverse the entire list of subsegments, and check each to see if it 
        /// is encroached. If so, add it to the list.
        /// </summary>
        void TallyEncs()
        {
            Osub subsegloop = default(Osub);
            int dummy;

            subsegloop.ssorient = 0;

            foreach (var s in mesh.subsegs.Values)
            {
                subsegloop.ss = s;
                // If the segment is encroached, add it to the list.
                dummy = CheckSeg4Encroach(ref subsegloop);
                //subsegloop.ss = subsegtraverse(m);
            }
        }

        /// <summary>
        /// Split all the encroached subsegments.
        /// </summary>
        /// <param name="triflaws">A flag that specifies whether one should take 
        /// note of new bad triangles that result from inserting vertices to repair 
        /// encroached subsegments.</param>
        /// <remarks>
        /// Each encroached subsegment is repaired by splitting it - inserting a
        /// vertex at or near its midpoint.  Newly inserted vertices may encroach
        /// upon other subsegments; these are also repaired.
        /// </remarks>
        void SplitEncSegs(bool triflaws)
        {
            Otri enctri = default(Otri);
            Otri testtri = default(Otri);
            Osub testsh = default(Osub);
            Osub currentenc = default(Osub);
            BadSubseg seg;
            Vertex eorg, edest, eapex;
            Vertex newvertex;
            InsertVertexResult success;
            double segmentlength, nearestpoweroftwo;
            double split;
            double multiplier, divisor;
            bool acuteorg, acuteorg2, acutedest, acutedest2;
            int dummy;

            // Note that steinerleft == -1 if an unlimited number
            // of Steiner points is allowed.
            while (badsubsegs.Count > 0)
            {
                if (mesh.steinerleft == 0)
                {
                    break;
                }

                seg = badsubsegs.Dequeue();

                currentenc = seg.encsubseg;
                eorg = currentenc.Org();
                edest = currentenc.Dest();
                // Make sure that this segment is still the same segment it was
                // when it was determined to be encroached.  If the segment was
                // enqueued multiple times (because several newly inserted
                // vertices encroached it), it may have already been split.
                if (!Osub.IsDead(currentenc.ss) && (eorg == seg.subsegorg) && (edest == seg.subsegdest))
                {
                    // To decide where to split a segment, we need to know if the
                    // segment shares an endpoint with an adjacent segment.
                    // The concern is that, if we simply split every encroached
                    // segment in its center, two adjacent segments with a small
                    // angle between them might lead to an infinite loop; each
                    // vertex added to split one segment will encroach upon the
                    // other segment, which must then be split with a vertex that
                    // will encroach upon the first segment, and so on forever.
                    // To avoid this, imagine a set of concentric circles, whose
                    // radii are powers of two, about each segment endpoint.
                    // These concentric circles determine where the segment is
                    // split. (If both endpoints are shared with adjacent
                    // segments, split the segment in the middle, and apply the
                    // concentric circles for later splittings.)

                    // Is the origin shared with another segment?
                    currentenc.TriPivot(ref enctri);
                    enctri.Lnext(ref testtri);
                    testtri.SegPivot(ref testsh);
                    acuteorg = testsh.ss != Mesh.dummysub;
                    // Is the destination shared with another segment?
                    testtri.LnextSelf();
                    testtri.SegPivot(ref testsh);
                    acutedest = testsh.ss != Mesh.dummysub;

                    // If we're using Chew's algorithm (rather than Ruppert's)
                    // to define encroachment, delete free vertices from the
                    // subsegment's diametral circle.
                    if (!Behavior.ConformDel && !acuteorg && !acutedest)
                    {
                        eapex = enctri.Apex();
                        while ((eapex.type == VertexType.FreeVertex) &&
                               ((eorg.pt.X - eapex.pt.X) * (edest.pt.X - eapex.pt.X) +
                                (eorg.pt.Y - eapex.pt.Y) * (edest.pt.Y - eapex.pt.Y) < 0.0))
                        {
                            mesh.DeleteVertex(ref testtri);
                            currentenc.TriPivot(ref enctri);
                            eapex = enctri.Apex();
                            enctri.Lprev(ref testtri);
                        }
                    }

                    // Now, check the other side of the segment, if there's a triangle there.
                    enctri.Sym(ref testtri);
                    if (testtri.triangle != Mesh.dummytri)
                    {
                        // Is the destination shared with another segment?
                        testtri.LnextSelf();
                        testtri.SegPivot(ref testsh);
                        acutedest2 = testsh.ss != Mesh.dummysub;
                        acutedest = acutedest || acutedest2;
                        // Is the origin shared with another segment?
                        testtri.LnextSelf();
                        testtri.SegPivot(ref testsh);
                        acuteorg2 = testsh.ss != Mesh.dummysub;
                        acuteorg = acuteorg || acuteorg2;

                        // Delete free vertices from the subsegment's diametral circle.
                        if (!Behavior.ConformDel && !acuteorg2 && !acutedest2)
                        {
                            eapex = testtri.Org();
                            while ((eapex.type == VertexType.FreeVertex) &&
                                   ((eorg.pt.X - eapex.pt.X) * (edest.pt.X - eapex.pt.X) +
                                    (eorg.pt.Y - eapex.pt.Y) * (edest.pt.Y - eapex.pt.Y) < 0.0))
                            {
                                mesh.DeleteVertex(ref testtri);
                                enctri.Sym(ref testtri);
                                eapex = testtri.Apex();
                                testtri.LprevSelf();
                            }
                        }
                    }

                    // Use the concentric circles if exactly one endpoint is shared
                    // with another adjacent segment.
                    if (acuteorg || acutedest)
                    {
                        segmentlength = Math.Sqrt((edest.pt.X - eorg.pt.X) * (edest.pt.X - eorg.pt.X) +
                                             (edest.pt.Y - eorg.pt.Y) * (edest.pt.Y - eorg.pt.Y));
                        // Find the power of two that most evenly splits the segment.
                        // The worst case is a 2:1 ratio between subsegment lengths.
                        nearestpoweroftwo = 1.0;
                        while (segmentlength > 3.0 * nearestpoweroftwo)
                        {
                            nearestpoweroftwo *= 2.0;
                        }
                        while (segmentlength < 1.5 * nearestpoweroftwo)
                        {
                            nearestpoweroftwo *= 0.5;
                        }
                        // Where do we split the segment?
                        split = nearestpoweroftwo / segmentlength;
                        if (acutedest)
                        {
                            split = 1.0 - split;
                        }
                    }
                    else
                    {
                        // If we're not worried about adjacent segments, split
                        // this segment in the middle.
                        split = 0.5;
                    }

                    // Create the new vertex.
                    newvertex = new Vertex(mesh.nextras);
                    mesh.vertices.Add(newvertex.Hash, newvertex);

                    // Interpolate its coordinate and attributes.
                    for (int i = 0; i < mesh.nextras; i++)
                    {
                        newvertex.attributes[i] = eorg.attributes[i]
                            + split * (edest.attributes[i] - eorg.attributes[i]);
                    }

                    newvertex.pt.X = eorg.pt.X + split * (edest.pt.X - eorg.pt.X);
                    newvertex.pt.Y = eorg.pt.Y + split * (edest.pt.Y - eorg.pt.Y);

                    if (!Behavior.NoExact)
                    {
                        // Roundoff in the above calculation may yield a 'newvertex'
                        // that is not precisely collinear with 'eorg' and 'edest'.
                        // Improve collinearity by one step of iterative refinement.
                        multiplier = Primitives.CounterClockwise(eorg.pt, edest.pt, newvertex.pt);
                        divisor = ((eorg.pt.X - edest.pt.X) * (eorg.pt.X - edest.pt.X) +
                                   (eorg.pt.Y - edest.pt.Y) * (eorg.pt.Y - edest.pt.Y));
                        if ((multiplier != 0.0) && (divisor != 0.0))
                        {
                            multiplier = multiplier / divisor;
                            // Watch out for NANs.
                            if (!double.IsNaN(multiplier))
                            {
                                newvertex.pt.X += multiplier * (edest.pt.Y - eorg.pt.Y);
                                newvertex.pt.Y += multiplier * (eorg.pt.X - edest.pt.X);
                            }
                        }
                    }

                    newvertex.mark = currentenc.Mark();
                    newvertex.type = VertexType.SegmentVertex;

                    // Check whether the new vertex lies on an endpoint.
                    if (((newvertex.pt.X == eorg.pt.X) && (newvertex.pt.Y == eorg.pt.Y)) ||
                        ((newvertex.pt.X == edest.pt.X) && (newvertex.pt.Y == edest.pt.Y)))
                    {

                        logger.Error("Ran out of precision: I attempted to split a"
                            + " segment to a smaller size than can be accommodated by"
                            + " the finite precision of floating point arithmetic.",
                            "Quality.SplitEncSegs()");

                        throw new Exception("Ran out of precision");
                    }
                    // Insert the splitting vertex.  This should always succeed.
                    success = mesh.InsertVertex(newvertex, ref enctri, ref currentenc, true, triflaws);
                    if ((success != InsertVertexResult.Successful) && (success != InsertVertexResult.Encroaching))
                    {
                        logger.Error("Failure to split a segment.", "Quality.SplitEncSegs()");
                        throw new Exception("Failure to split a segment.");
                    }
                    if (mesh.steinerleft > 0)
                    {
                        mesh.steinerleft--;
                    }
                    // Check the two new subsegments to see if they're encroached.
                    dummy = CheckSeg4Encroach(ref currentenc);
                    currentenc.NextSelf();
                    dummy = CheckSeg4Encroach(ref currentenc);
                }

                // Set subsegment's origin to NULL. This makes it possible to detect dead 
                // badsubsegs when traversing the list of all badsubsegs.
                seg.subsegorg = null;
            }
        }

        /// <summary>
        /// Test every triangle in the mesh for quality measures.
        /// </summary>
        void TallyFaces()
        {
            Otri triangleloop = default(Otri);

            triangleloop.orient = 0;

            foreach (var t in mesh.triangles.Values)
            {
                triangleloop.triangle = t;

                // If the triangle is bad, enqueue it.
                TestTriangle(ref triangleloop);
            }
        }

        /// <summary>
        /// Inserts a vertex at the circumcenter of a triangle. Deletes 
        /// the newly inserted vertex if it encroaches upon a segment.
        /// </summary>
        /// <param name="badtri"></param>
        void SplitTriangle(BadTriangle badtri)
        {
            Otri badotri = default(Otri);
            Vertex borg, bdest, bapex;
            Vertex newvertex;
            double xi = 0, eta = 0;
            InsertVertexResult success;
            bool errorflag;
            int i;

            badotri = badtri.poortri;
            borg = badotri.Org();
            bdest = badotri.Dest();
            bapex = badotri.Apex();

            // Make sure that this triangle is still the same triangle it was
            // when it was tested and determined to be of bad quality.
            // Subsequent transformations may have made it a different triangle.
            if (!Otri.IsDead(badotri.triangle) && (borg == badtri.triangorg) &&
                (bdest == badtri.triangdest) && (bapex == badtri.triangapex))
            {
                errorflag = false;
                // Create a new vertex at the triangle's circumcenter.
                newvertex = new Vertex(mesh.nextras);

                // Using the original (simpler) Steiner point location method
                // for mesh refinement.
                // TODO: NewLocation doesn't work for refinement. Why? Maybe 
                // reset VertexType?
                if (Behavior.FixedArea || Behavior.VarArea)
                {
                    newvertex.pt = Primitives.FindCircumcenter(borg.pt, bdest.pt, bapex.pt, ref xi, ref eta, true);
                }
                else
                {
                    NewLocation.FindLocation(mesh, borg, bdest, bapex, newvertex, ref xi, ref eta, true, badotri);
                }

                // Check whether the new vertex lies on a triangle vertex.
                if (((newvertex.pt.X == borg.pt.X) && (newvertex.pt.Y == borg.pt.Y)) ||
                    ((newvertex.pt.X == bdest.pt.X) && (newvertex.pt.Y == bdest.pt.Y)) ||
                    ((newvertex.pt.X == bapex.pt.X) && (newvertex.pt.Y == bapex.pt.Y)))
                {
                    if (Behavior.Verbose)
                    {
                        logger.Warning("New vertex falls on existing vertex.", "Quality.SplitTriangle()");
                        errorflag = true;
                    }
                }
                else
                {
                    for (i = 0; i < mesh.nextras; i++)
                    {
                        // Interpolate the vertex attributes at the circumcenter.
                        newvertex.attributes[i] = borg.attributes[i]
                            + xi * (bdest.attributes[i] - borg.attributes[i])
                            + eta * (bapex.attributes[i] - borg.attributes[i]);
                    }
                    // The new vertex must be in the interior, and therefore is a
                    // free vertex with a marker of zero.
                    newvertex.mark = 0;
                    newvertex.type = VertexType.FreeVertex;

                    // Ensure that the handle 'badotri' does not represent the longest
                    // edge of the triangle.  This ensures that the circumcenter must
                    // fall to the left of this edge, so point location will work.
                    // (If the angle org-apex-dest exceeds 90 degrees, then the
                    // circumcenter lies outside the org-dest edge, and eta is
                    // negative.  Roundoff error might prevent eta from being
                    // negative when it should be, so I test eta against xi.)
                    if (eta < xi)
                    {
                        badotri.LprevSelf();
                    }

                    // Insert the circumcenter, searching from the edge of the triangle,
                    // and maintain the Delaunay property of the triangulation.
                    Osub tmp = default(Osub);
                    success = mesh.InsertVertex(newvertex, ref badotri, ref tmp, true, true);

                    if (success == InsertVertexResult.Successful)
                    {
                        mesh.vertices.Add(newvertex.Hash, newvertex);

                        if (mesh.steinerleft > 0)
                        {
                            mesh.steinerleft--;
                        }
                    }
                    else if (success == InsertVertexResult.Encroaching)
                    {
                        // If the newly inserted vertex encroaches upon a subsegment,
                        // delete the new vertex.
                        mesh.UndoVertex();
                    }
                    else if (success == InsertVertexResult.Violating)
                    {
                        // Failed to insert the new vertex, but some subsegment was
                        // marked as being encroached.
                    }
                    else
                    {   // success == DUPLICATEVERTEX
                        // Couldn't insert the new vertex because a vertex is already there.
                        if (Behavior.Verbose)
                        {
                            logger.Warning("New vertex falls on existing vertex.", "Quality.SplitTriangle()");
                            errorflag = true;
                        }
                    }
                }
                if (errorflag)
                {
                    logger.Error("The new vertex is at the circumcenter of triangle: This probably "
                        + "means that I am trying to refine triangles to a smaller size than can be "
                        + "accommodated by the finite precision of floating point arithmetic.",
                        "Quality.SplitTriangle()");

                    throw new Exception("The new vertex is at the circumcenter of triangle.");
                }
            }
        }

        /// <summary>
        /// Remove all the encroached subsegments and bad triangles from the triangulation.
        /// </summary>
        public void EnforceQuality()
        {
            BadTriangle badtri;

            // Test all segments to see if they're encroached.
            TallyEncs();

            // Fix encroached subsegments without noting bad triangles.
            SplitEncSegs(false);
            // At this point, if we haven't run out of Steiner points, the
            // triangulation should be (conforming) Delaunay.

            // Next, we worry about enforcing triangle quality.
            if ((Behavior.MinAngle > 0.0) || Behavior.VarArea || Behavior.FixedArea || Behavior.Usertest)
            {
                // TODO: Reset queue? (Or is it always empty at this point)

                // Test all triangles to see if they're bad.
                TallyFaces();

                mesh.checkquality = true;
                while ((queue.badtriangles.Count > 0) && (mesh.steinerleft != 0))
                {
                    // Fix one bad triangle by inserting a vertex at its circumcenter.
                    badtri = queue.Dequeue();
                    SplitTriangle(badtri);

                    if (badsubsegs.Count > 0)
                    {
                        // Put bad triangle back in queue for another try later.
                        queue.Enqueue(badtri);
                        // Fix any encroached subsegments that resulted.
                        // Record any new bad triangles that result.
                        SplitEncSegs(true);
                    }
                    else
                    {
                        // Return the bad triangle to the pool.
                        queue.badtriangles.Remove(badtri);
                    }
                }
            }

            // At this point, if the "-D" switch was selected and we haven't run out
            // of Steiner points, the triangulation should be (conforming) Delaunay
            // and have no low-quality triangles.

            // Might we have run out of Steiner points too soon?
            if (Behavior.Verbose && Behavior.ConformDel && (badsubsegs.Count > 0) && (mesh.steinerleft == 0))
            {
                
                logger.Warning("I ran out of Steiner points, but the mesh has encroached subsegments, "
                        + "and therefore might not be truly Delaunay. If the Delaunay property is important "
                        + "to you, try increasing the number of Steiner points", 
                        "Quality.EnforceQuality()");
            }
        }
        #endregion
    }
}
