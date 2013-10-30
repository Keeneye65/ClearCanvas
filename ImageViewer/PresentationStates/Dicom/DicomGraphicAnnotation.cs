#region License

// Copyright (c) 2013, ClearCanvas Inc.
// All rights reserved.
// http://www.clearcanvas.ca
//
// This file is part of the ClearCanvas RIS/PACS open source project.
//
// The ClearCanvas RIS/PACS open source project is free software: you can
// redistribute it and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// The ClearCanvas RIS/PACS open source project is distributed in the hope that it
// will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General
// Public License for more details.
//
// You should have received a copy of the GNU General Public License along with
// the ClearCanvas RIS/PACS open source project.  If not, see
// <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using ClearCanvas.Common;
using ClearCanvas.Common.Utilities;
using ClearCanvas.Dicom.Iod.Sequences;
using ClearCanvas.ImageViewer.Graphics;
using ClearCanvas.ImageViewer.InteractiveGraphics;
using ClearCanvas.ImageViewer.Mathematics;

namespace ClearCanvas.ImageViewer.PresentationStates.Dicom
{
	/// <summary>
	/// A <see cref="IGraphic"/> whose contents represent those of a DICOM Graphic Annotation Sequence (PS 3.3 C.10.5).
	/// </summary>
	[Cloneable]
	[DicomSerializableGraphicAnnotation(typeof (DicomGraphicAnnotationSerializer))]
	public class DicomGraphicAnnotation : CompositeGraphic
	{
		private string _layerId;
		private Color _color = Color.LemonChiffon;

		/// <summary>
		/// Constructs a new <see cref="IGraphic"/> whose contents are constructed based on a <see cref="GraphicAnnotationSequenceItem">DICOM Graphic Annotation Sequence Item</see>.
		/// </summary>
		/// <param name="graphicAnnotationSequenceItem">The DICOM graphic annotation sequence item to render.</param>
		/// <param name="displayedArea">The image's displayed area with which to </param>
		/// <param name="editable">Indicates whether or not the graphic should be interactive and editable by the user.</param>
		public DicomGraphicAnnotation(GraphicAnnotationSequenceItem graphicAnnotationSequenceItem, RectangleF displayedArea, bool editable = false)
		{
			this.CoordinateSystem = CoordinateSystem.Source;
			_layerId = graphicAnnotationSequenceItem.GraphicLayer ?? string.Empty;

			try
			{
				List<PointF> dataPoints = new List<PointF>();
				if (graphicAnnotationSequenceItem.GraphicObjectSequence != null)
				{
					foreach (GraphicAnnotationSequenceItem.GraphicObjectSequenceItem graphicItem in graphicAnnotationSequenceItem.GraphicObjectSequence)
					{
						try
						{
							IList<PointF> points = GetGraphicDataAsSourceCoordinates(displayedArea, graphicItem);
							var graphic = CreateGraphic(graphicItem.GraphicType, points, editable);
							if (graphic != null)
							{
								if (editable) graphic = new StandardStatefulGraphic(graphic);
								Graphics.Add(graphic);
							}
							dataPoints.AddRange(points);
						}
						catch (Exception ex)
						{
							Platform.Log(LogLevel.Warn, ex, "DICOM Softcopy Presentation State Deserialization Fault (Graphic Object Type {0}). Reprocess with log level DEBUG to see DICOM data dump.", graphicItem.GraphicType);
							Platform.Log(LogLevel.Debug, graphicItem.DicomSequenceItem.Dump());
						}
					}
				}

				RectangleF annotationBounds = RectangleF.Empty;
				if (dataPoints.Count > 0)
					annotationBounds = RectangleUtilities.ComputeBoundingRectangle(dataPoints.ToArray());
				if (graphicAnnotationSequenceItem.TextObjectSequence != null)
				{
					foreach (GraphicAnnotationSequenceItem.TextObjectSequenceItem textItem in graphicAnnotationSequenceItem.TextObjectSequence)
					{
						try
						{
							base.Graphics.Add(CreateCalloutText(annotationBounds, displayedArea, textItem));
						}
						catch (Exception ex)
						{
							Platform.Log(LogLevel.Warn, ex, "DICOM Softcopy Presentation State Deserialization Fault (Text Object). Reprocess with log level DEBUG to see DICOM data dump.");
							Platform.Log(LogLevel.Debug, textItem.DicomSequenceItem.Dump());
						}
					}
				}
			}
			finally
			{
				this.ResetCoordinateSystem();
			}

			OnColorChanged();
		}

		/// <summary>
		/// Cloning constructor.
		/// </summary>
		protected DicomGraphicAnnotation(DicomGraphicAnnotation source, ICloningContext context)
		{
			context.CloneFields(source, this);
		}

		/// <summary>
		/// Gets the layer ID to which this graphic annotation belongs.
		/// </summary>
		public string LayerId
		{
			get { return _layerId; }
		}

		/// <summary>
		/// Gets or sets the <see cref="CompositeGraphic.CoordinateSystem"/>.
		/// </summary>
		/// <remarks>
		/// Setting the <see cref="CompositeGraphic.CoordinateSystem"/> property will recursively set the 
		/// <see cref="CompositeGraphic.CoordinateSystem"/> property for <i>all</i> <see cref="Graphic"/> 
		/// objects in the subtree.
		/// </remarks>
		public override sealed CoordinateSystem CoordinateSystem
		{
			get { return base.CoordinateSystem; }
			set { base.CoordinateSystem = value; }
		}

		/// <summary>
		/// Resets the <see cref="CompositeGraphic.CoordinateSystem"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// <see cref="CompositeGraphic.ResetCoordinateSystem"/> will reset the <see cref="CompositeGraphic.CoordinateSystem"/>
		/// to what it was before the <see cref="CompositeGraphic.CoordinateSystem"/> was last set.
		/// </para>
		/// <para>
		/// Calling <see cref="CompositeGraphic.ResetCoordinateSystem"/> will recursively call
		/// <see cref="CompositeGraphic.ResetCoordinateSystem"/> on <i>all</i> <see cref="Graphic"/> 
		/// objects in the subtree.
		/// </para>
		/// </remarks>
		public override sealed void ResetCoordinateSystem()
		{
			base.ResetCoordinateSystem();
		}

		/// <summary>
		/// Gets or sets the color of this graphic annotation.
		/// </summary>
		public Color Color
		{
			get { return _color; }
			set
			{
				if (_color != value)
				{
					_color = value;
					OnColorChanged();
				}
			}
		}

		/// <summary>
		/// Called when the <see cref="DicomGraphicAnnotation.Color"/> property changes.
		/// </summary>
		protected void OnColorChanged()
		{
			foreach (IGraphic graphic in base.Graphics)
			{
				if (graphic is IVectorGraphic)
					((IVectorGraphic) graphic).Color = _color;
				else if (graphic is StandardStatefulGraphic)
					((StandardStatefulGraphic) graphic).InactiveColor = ((StandardStatefulGraphic) graphic).Color = _color;
			}
		}

		#region Static Graphic Creation Helpers

		private static IList<PointF> GetGraphicDataAsSourceCoordinates(RectangleF displayedArea, GraphicAnnotationSequenceItem.GraphicObjectSequenceItem graphicItem)
		{
			List<PointF> list;
			if (graphicItem.GraphicAnnotationUnits == GraphicAnnotationSequenceItem.GraphicAnnotationUnits.Display)
			{
				list = new List<PointF>(graphicItem.NumberOfGraphicPoints);
				foreach (PointF point in graphicItem.GraphicData)
					list.Add(GetPointInSourceCoordinates(displayedArea, point));
			}
			else
			{
				// offset to account for our 0,0 origin versyus DICOM 1,1 origin
				list = new List<PointF>(graphicItem.NumberOfGraphicPoints);
				foreach (PointF point in graphicItem.GraphicData)
					list.Add(new PointF(point.X - 1, point.Y - 1));
			}
			return list.AsReadOnly();
		}

		private static PointF GetPointInSourceCoordinates(RectangleF displayedArea, PointF point)
		{
			return displayedArea.Location + new SizeF(displayedArea.Width*point.X, displayedArea.Height*point.Y);
		}

		private static IGraphic CreateGraphic(GraphicAnnotationSequenceItem.GraphicType graphicType, IList<PointF> points, bool editable = false)
		{
			switch (graphicType)
			{
				case GraphicAnnotationSequenceItem.GraphicType.Interpolated:
					Platform.CheckTrue(points.Count >= 2, "Graphic Type INTERPOLATED requires at least 2 coordinates");
					return CreateInterpolated(points, editable);
				case GraphicAnnotationSequenceItem.GraphicType.Polyline:
					Platform.CheckTrue(points.Count >= 2, "Graphic Type POLYLINE requires at least 2 coordinates");
					return CreatePolyline(points, editable);
				case GraphicAnnotationSequenceItem.GraphicType.Point:
					Platform.CheckTrue(points.Count >= 1, "Graphic Type POINT requires 1 coordinate");
					return CreatePoint(points[0], editable);
				case GraphicAnnotationSequenceItem.GraphicType.Circle:
					Platform.CheckTrue(points.Count >= 2, "Graphic Type CIRCLE requires 2 coordinates");
					return CreateCircle(points[0], (float) Vector.Distance(points[0], points[1]), editable);
				case GraphicAnnotationSequenceItem.GraphicType.Ellipse:
					Platform.CheckTrue(points.Count >= 4, "Graphic Type INTERPOLATED requires 4 coordinates");
					return CreateEllipse(points[0], points[1], points[2], points[3], editable);
				default:
					Platform.Log(LogLevel.Debug, "Unrecognized Graphic Type");
					return null;
			}
		}

		private static IGraphic CreateInterpolated(IList<PointF> dataPoints, bool editable = false)
		{
			var curve = new CurvePrimitive();
			curve.Points.AddRange(dataPoints);
			return editable ? (IGraphic) new VerticesControlGraphic(new MoveControlGraphic(curve)) : curve;
		}

		private static IGraphic CreatePolyline(IList<PointF> vertices, bool editable = false)
		{
			var closed = FloatComparer.AreEqual(vertices[0], vertices[vertices.Count - 1]);
			var polyline = new PolylineGraphic(closed);
			polyline.Points.AddRange(vertices);
			if (!editable) return polyline;
			return closed ? new PolygonControlGraphic(true, new MoveControlGraphic(polyline)) : new VerticesControlGraphic(true, new MoveControlGraphic(polyline));
		}

		private static IGraphic CreateCircle(PointF center, float radius, bool editable = false)
		{
			var circle = new EllipsePrimitive();
			var radial = new SizeF(radius, radius);
			circle.TopLeft = center - radial;
			circle.BottomRight = center + radial;
			return editable ? (IGraphic) new BoundableResizeControlGraphic(new BoundableStretchControlGraphic(new MoveControlGraphic(circle))) : circle;
		}

		private static IGraphic CreatePoint(PointF location, bool editable = false)
		{
			const float radius = 4;

			var point = new InvariantEllipsePrimitive();
			point.Location = location;
			point.InvariantTopLeft = new PointF(-radius, -radius);
			point.InvariantBottomRight = new PointF(radius, radius);
			return editable ? (IGraphic) new MoveControlGraphic(point) : point;
		}

		private static IGraphic CreateEllipse(PointF majorAxisEnd1, PointF majorAxisEnd2, PointF minorAxisEnd1, PointF minorAxisEnd2, bool editable = false)
		{
			var dicomEllipseGraphic = new DicomEllipseGraphic();
			dicomEllipseGraphic.MajorAxisPoint1 = majorAxisEnd1;
			dicomEllipseGraphic.MajorAxisPoint2 = majorAxisEnd2;
			dicomEllipseGraphic.MinorAxisPoint1 = minorAxisEnd1;
			dicomEllipseGraphic.MinorAxisPoint2 = minorAxisEnd2;
			return editable ? (IGraphic) (new MoveControlGraphic(dicomEllipseGraphic)) : dicomEllipseGraphic;
		}

		private static IGraphic CreateCalloutText(RectangleF annotationBounds, RectangleF displayedArea, GraphicAnnotationSequenceItem.TextObjectSequenceItem textItem)
		{
			if (textItem.AnchorPoint.HasValue)
			{
				CalloutGraphic callout = new CalloutGraphic(textItem.UnformattedTextValue);

				PointF anchor = textItem.AnchorPoint.Value;
				if (textItem.AnchorPointAnnotationUnits == GraphicAnnotationSequenceItem.AnchorPointAnnotationUnits.Display)
					anchor = GetPointInSourceCoordinates(displayedArea, anchor);

				callout.AnchorPoint = anchor;
				callout.ShowArrowhead = annotationBounds.IsEmpty; // show arrowhead if graphic annotation bounds are empty

				if (textItem.BoundingBoxTopLeftHandCorner.HasValue && textItem.BoundingBoxBottomRightHandCorner.HasValue)
				{
					PointF topLeft = textItem.BoundingBoxTopLeftHandCorner.Value;
					PointF bottomRight = textItem.BoundingBoxBottomRightHandCorner.Value;

					if (textItem.BoundingBoxAnnotationUnits == GraphicAnnotationSequenceItem.BoundingBoxAnnotationUnits.Display)
					{
						topLeft = GetPointInSourceCoordinates(displayedArea, topLeft);
						bottomRight = GetPointInSourceCoordinates(displayedArea, bottomRight);
					}

					callout.TextLocation = Vector.Midpoint(topLeft, bottomRight);
				}
				else
				{
					if (!annotationBounds.IsEmpty)
						callout.TextLocation = annotationBounds.Location - new SizeF(30, 30);
					else
						callout.TextLocation = anchor - new SizeF(30, 30);
				}

				StandardStatefulGraphic statefulCallout = new StandardStatefulGraphic(callout);
				statefulCallout.InactiveColor = Color.LemonChiffon;
				statefulCallout.State = statefulCallout.CreateInactiveState();
				return statefulCallout;
			}
			else if (textItem.BoundingBoxTopLeftHandCorner.HasValue && textItem.BoundingBoxBottomRightHandCorner.HasValue)
			{
				InvariantTextPrimitive text = new InvariantTextPrimitive(textItem.UnformattedTextValue);
				PointF topLeft = textItem.BoundingBoxTopLeftHandCorner.Value;
				PointF bottomRight = textItem.BoundingBoxBottomRightHandCorner.Value;

				if (textItem.BoundingBoxAnnotationUnits == GraphicAnnotationSequenceItem.BoundingBoxAnnotationUnits.Display)
				{
					topLeft = GetPointInSourceCoordinates(displayedArea, topLeft);
					bottomRight = GetPointInSourceCoordinates(displayedArea, bottomRight);
				}

				// TODO: make the text variant - rotated as specified by bounding area - as well as justified as requested
				// RectangleF boundingBox = RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
				// boundingBox = RectangleUtilities.ConvertToPositiveRectangle(boundingBox);
				// boundingBox.Location = boundingBox.Location - new SizeF(1, 1);
				text.Location = Vector.Midpoint(topLeft, bottomRight);

				return text;
			}
			else
			{
				throw new InvalidDataException("The GraphicAnnotationSequenceItem must define either an anchor point or a bounding box.");
			}
		}

		#endregion

		private class DicomGraphicAnnotationSerializer : GraphicAnnotationSerializer<DicomGraphicAnnotation>
		{
			protected override void Serialize(DicomGraphicAnnotation graphic, GraphicAnnotationSequenceItem serializationState)
			{
				foreach (IGraphic subgraphic in graphic.Graphics)
					SerializeGraphic(subgraphic, serializationState);
			}
		}
	}
}