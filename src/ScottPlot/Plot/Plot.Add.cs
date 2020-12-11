﻿/* This file contains helper methods for creating plottables, customizing them based on optional arguments, 
 * adding them to the plot, then returning them for additional customization all with a single method call.
 * 
 * Plottable-creating helper methods try to obey these rules:
 * 
 *   1. Only the most common plot types get helper methods.
 *      Uncommon or experimental plottables can be created by the user and added with Add().
 *   
 *   2. Only the most common styling options are configurable with optional arguments.
 *      This is subjective, but guided by what is in the cookbook and often seen in the wild.
 *      Plottables are always returned by helper methods, so users can customize them extensively as desired.
 *   
 */

using ScottPlot.Plottable;
using ScottPlot.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace ScottPlot
{
    public partial class Plot
    {

        /// <summary>
        /// Display text in the data area at a pixel location (not a X/Y coordinates)
        /// </summary>
        public Annotation AddAnnotation(string label, double x, double y, float size = 12, Color? color = null, Color? backColor = null)
        {
            var plottable = new Annotation()
            {
                Label = label,
                X = x,
                Y = y,
                FontSize = size
            };
            plottable.Font.Color = color ?? plottable.Font.Color;
            plottable.BackgroundColor = backColor ?? plottable.BackgroundColor;
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Display an arrow pointing to a spot in coordinate space
        /// </summary>
        public ScatterPlot AddArrow(double xTip, double yTip, double xBase, double yBase, float lineWidth = 5, Color? color = null)
        {
            double[] xs = { xBase, xTip };
            double[] ys = { yBase, yTip };
            var plottable = new ScatterPlot(xs, ys)
            {
                lineWidth = lineWidth,
                markerSize = 0,
                color = color ?? GetNextColor(),
                ArrowheadLength = 3,
                ArrowheadWidth = 3
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a bar plot for the given values. Bars will be placed at X positions 0, 1, 2, etc.
        /// </summary>
        public BarPlot AddBar(double[] values, Color? color = null)
        {
            double[] xs = DataGen.Consecutive(values.Length);
            var plottable = new BarPlot(xs, values, null, null)
            {
                fillColor = color ?? GetNextColor()
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a bar plot for the given values using defined bar positions
        /// </summary>
        public BarPlot AddBar(double[] values, double[] positions, Color? color = null)
        {
            var plottable = new BarPlot(positions, values, null, null)
            {
                fillColor = color ?? GetNextColor()
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a bar plot (values +/- errors) using defined positions
        /// </summary>
        public BarPlot AddBar(double[] values, double[] errors, double[] positions, Color? color = null)
        {
            var plottable = new BarPlot(positions, values, errors, null)
            {
                fillColor = color ?? GetNextColor()
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Create a series of bar plots and customize the ticks and legend
        /// </summary>
        public BarPlot[] AddBarGroups(string[] groupLabels, string[] seriesLabels, double[][] ys, double[][] yErr)
        {
            if (groupLabels is null || seriesLabels is null || ys is null)
                throw new ArgumentException("labels and ys cannot be null");

            if (seriesLabels.Length != ys.Length)
                throw new ArgumentException("groupLabels and ys must be the same length");

            foreach (double[] subArray in ys)
                if (subArray.Length != groupLabels.Length)
                    throw new ArgumentException("all arrays inside ys must be the same length as groupLabels");

            double groupWidthFraction = 0.8;
            double barWidthFraction = 0.8;
            double errorCapSize = 0.38;

            int seriesCount = ys.Length;
            double barWidth = groupWidthFraction / seriesCount;
            BarPlot[] bars = new BarPlot[seriesCount];
            bool containsNegativeY = false;
            for (int i = 0; i < seriesCount; i++)
            {
                double[] barYs = ys[i];
                double[] barYerr = yErr?[i];
                double[] barXs = DataGen.Consecutive(barYs.Length);
                containsNegativeY |= barYs.Where(y => y < 0).Any();
                var bar = new BarPlot(barXs, barYs, barYerr, null)
                {
                    label = seriesLabels[i],
                    barWidth = barWidth * barWidthFraction,
                    xOffset = i * barWidth,
                    errorCapSize = errorCapSize,
                    fillColor = GetNextColor()
                };
                Add(bar);
            }

            if (containsNegativeY)
                AxisAuto();

            double[] groupPositions = DataGen.Consecutive(groupLabels.Length, offset: (groupWidthFraction - barWidth) / 2);
            XTicks(groupPositions, groupLabels);

            return bars;
        }

        /// <summary>
        /// Add candlesticks to the chart from OHLC (open, high, low, close) data
        /// </summary>
        public FinancePlot AddCandlesticks(OHLC[] ohlcs)
        {
            FinancePlot plottable = new FinancePlot()
            {
                ohlcs = ohlcs,
                Candle = true,
                ColorUp = ColorTranslator.FromHtml("#26a69a"),
                ColorDown = ColorTranslator.FromHtml("#ef5350"),
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Create a polygon to fill the area between Y values and a baseline.
        /// </summary>
        public Polygon AddFill(double[] xs, double[] ys, double baseline = 0, Color? color = null)
        {
            var plottable = new Polygon(
                xs: Tools.Pad(xs, cloneEdges: true),
                ys: Tools.Pad(ys, 1, baseline, baseline))
            {
                fill = true,
                fillColor = color ?? GetNextColor(.5),
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Create a polygon to fill the area between Y values of two curves.
        /// </summary>
        public Polygon AddFill(double[] xs1, double[] ys1, double[] xs2, double[] ys2, Color? color = null)
        {
            // combine xs and ys to make one big curve
            int pointCount = xs1.Length + xs2.Length;
            double[] bothX = new double[pointCount];
            double[] bothY = new double[pointCount];

            // copy the first dataset as-is
            Array.Copy(xs1, 0, bothX, 0, xs1.Length);
            Array.Copy(ys1, 0, bothY, 0, ys1.Length);

            // copy the second dataset in reverse order
            for (int i = 0; i < xs2.Length; i++)
            {
                bothX[xs1.Length + i] = xs2[xs2.Length - 1 - i];
                bothY[ys1.Length + i] = ys2[ys2.Length - 1 - i];
            }

            var plottable = new Polygon(bothX, bothY)
            {
                fill = true,
                fillColor = color ?? GetNextColor(.5),
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Create a polygon to fill the area between Y values and a baseline
        /// that uses two different colors for area above and area below the baseline.
        /// </summary>
        public (Polygon polyAbove, Polygon polyBelow) AddFillAboveAndBelow(double[] xs, double[] ys, double baseline = 0, Color? colorAbove = null, Color? colorBelow = null)
        {
            // TODO: this almost works perfectly, but not quite.
            // look at scatter plots with low numbers of points
            // that cross the baseline a lot. The same X value appears
            // to have filled area both above and below the curve.

            double[] xs2 = Tools.Pad(xs, cloneEdges: true);
            double[] ys2 = Tools.Pad(ys, padWithLeft: baseline, padWithRight: baseline);
            double[] ys2below = new double[ys2.Length];
            double[] ys2above = new double[ys2.Length];

            for (int i = 0; i < ys2.Length; i++)
            {
                if (ys2[i] < baseline)
                {
                    ys2below[i] = ys2[i];
                    ys2above[i] = baseline;
                }
                else
                {
                    ys2above[i] = ys2[i];
                    ys2below[i] = baseline;
                }
            }

            Polygon polyAbove = new Polygon(xs2, ys2above);
            Polygon polyBelow = new Polygon(xs2, ys2below);

            polyAbove.fillColor = colorAbove ?? Color.Green;
            polyBelow.fillColor = colorBelow ?? Color.Red;

            Add(polyAbove);
            Add(polyBelow);

            return (polyAbove, polyBelow);
        }

        /// <summary>
        /// Add a line plot that uses a function (rather than X/Y points) to place the curve
        /// </summary>
        public FunctionPlot AddFunction(Func<double, double?> function, Color? color = null, double lineWidth = 1, LineStyle lineStyle = LineStyle.Solid)
        {
            FunctionPlot plottable = new FunctionPlot(function)
            {
                color = color ?? settings.GetNextColor(),
                lineWidth = lineWidth,
                lineStyle = lineStyle
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a horizontal axis line at a specific Y position
        /// </summary>
        public HLine AddHorizontalLine(double y, Color? color = null, float width = 1, LineStyle style = LineStyle.Solid, string label = null)
        {
            HLine plottable = new HLine()
            {
                Y = y,
                Color = color ?? settings.GetNextColor(),
                LineWidth = width,
                LineStyle = style,
                Label = label,
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a horizontal span (shades the region between two X positions)
        /// </summary>
        public HSpan AddHorizontalSpan(double xMin, double xMax, Color? color = null, string label = null)
        {
            var plottable = new HSpan()
            {
                position1 = xMin,
                position2 = xMax,
                color = color ?? GetNextColor(.5),
                label = label,
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a line (a scatter plot with two points) to the plot
        /// </summary>
        public ScatterPlot AddLine(double x1, double y1, double x2, double y2, Color? color = null, float lineWidth = 1)
        {
            return AddScatter(new double[] { x1, x2 }, new double[] { y1, y2 }, color, lineWidth, 0);
        }

        /// <summary>
        /// Add a line (a scatter plot with two points) to the plot
        /// </summary>
        public ScatterPlot AddLine(double slope, double offset, (double x1, double x2) xLimits, Color? color = null, float lineWidth = 1)
        {
            double y1 = xLimits.x1 * slope + offset;
            double y2 = xLimits.x2 * slope + offset;
            return AddScatter(new double[] { xLimits.x1, xLimits.x2 }, new double[] { y1, y2 }, color, lineWidth, 0);
        }

        /// <summary>
        /// Add OHLC (open, high, low, close) data to the plot
        /// </summary>
        public FinancePlot AddOHLCs(OHLC[] ohlcs)
        {
            FinancePlot plottable = new FinancePlot()
            {
                ohlcs = ohlcs,
                Candle = false,
                ColorUp = ColorTranslator.FromHtml("#26a69a"),
                ColorDown = ColorTranslator.FromHtml("#ef5350"),
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a pie chart to the plot
        /// </summary>
        public PiePlot AddPie(double[] values, bool hideGridAndFrame = true)
        {
            Color[] colors = Enumerable.Range(0, values.Length)
                                       .Select(i => settings.PlottablePalette.GetColor(i))
                                       .ToArray();

            PiePlot pie = new PiePlot(values, null, colors);
            Add(pie);

            if (hideGridAndFrame)
            {
                Grid(false);
                Frameless();
            }

            return pie;
        }

        /// <summary>
        /// Add a point (a scatter plot with a single marker)
        /// </summary>
        public ScatterPlot AddPoint(double x, double y, Color? color = null, float size = 5, MarkerShape shape = MarkerShape.filledCircle)
        {
            var plottable = new ScatterPlot(new double[] { x }, new double[] { y })
            {
                color = color ?? settings.GetNextColor(),
                markerSize = size,
                markerShape = shape
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a polygon to the plot
        /// </summary>
        public Polygon AddPolygon(double[] xs, double[] ys, Color? fillColor = null, double lineWidth = 0, Color? lineColor = null)
        {
            var plottable = new Polygon(xs, ys)
            {
                lineWidth = lineWidth,
                lineColor = lineColor ?? Color.Black,
                fillColor = fillColor ?? settings.GetNextColor(),
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add many polygons using an optimized rendering method
        /// </summary>
        public Polygons AddPolygons(List<List<(double x, double y)>> polys, Color? fillColor = null, double lineWidth = 0, Color? lineColor = null)
        {
            var plottable = new Polygons(polys)
            {
                lineWidth = lineWidth,
                lineColor = lineColor ?? Color.Black,
                fillColor = fillColor ?? settings.GetNextColor(),
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a population to the plot
        /// </summary>
        public PopulationPlot AddPopulation(Population population, string label = null)
        {
            var plottable = new PopulationPlot(population, label, settings.GetNextColor());
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add multiple populations to the plot as a single series
        /// </summary>
        public PopulationPlot AddPopulations(Population[] populations, string label = null)
        {
            var plottable = new PopulationPlot(populations, label, settings.GetNextColor());
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add multiple populations to the plot as a single series
        /// </summary>
        public PopulationPlot AddPopulations(PopulationMultiSeries multiSeries)
        {
            for (int i = 0; i < multiSeries.multiSeries.Length; i++)
                multiSeries.multiSeries[i].color = settings.PlottablePalette.GetColor(i);

            var plottable = new PopulationPlot(multiSeries);
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a radar plot
        /// </summary>
        public RadarPlot AddRadar(double[,] values, bool independentAxes = false, double[] maxValues = null, bool disableFrameAndGrid = true)
        {

            Color[] colors = Enumerable.Range(0, values.Length)
                                       .Select(i => settings.PlottablePalette.GetColor(i))
                                       .ToArray();

            Color[] fills = colors.Select(x => Color.FromArgb(50, x)).ToArray();

            RadarPlot plottable = new RadarPlot(values, colors, fills, independentAxes, maxValues);
            Add(plottable);

            if (disableFrameAndGrid)
            {
                Frameless();
                Grid(enable: false);
            }

            return plottable;
        }

        /// <summary>
        /// Add an L-shaped scalebar to the corner of the plot
        /// </summary>
        public ScaleBar AddScaleBar(double width, double height, string xLabel = null, string yLabel = null)
        {
            var scalebar = new ScaleBar()
            {
                Width = width,
                Height = height,
                HorizontalLabel = xLabel,
                VerticalLabel = yLabel,
            };
            Add(scalebar);
            return scalebar;
        }

        /// <summary>
        /// Add a scatter plot from X/Y pairs. 
        /// Scatter plots are slower than Signal plots.
        /// </summary>
        public ScatterPlot AddScatter(
            double[] xs,
            double[] ys,
            Color? color = null,
            float lineWidth = 1,
            float markerSize = 5,
            string label = null,
            MarkerShape markerShape = MarkerShape.filledCircle,
            LineStyle lineStyle = LineStyle.Solid)
        {
            var plottable = new ScatterPlot(xs, ys, null, null)
            {
                color = color ?? GetNextColor(),
                lineWidth = lineWidth,
                markerSize = markerSize,
                label = label,
                markerShape = markerShape,
                lineStyle = lineStyle
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Scatter plot with Add() and Clear() methods for updating data
        /// </summary>
        public ScatterPlotList AddScatterList(
            Color? color = null,
            float lineWidth = 1,
            float markerSize = 5,
            string label = null,
            MarkerShape markerShape = MarkerShape.filledCircle,
            LineStyle lineStyle = LineStyle.Solid)
        {
            var spl = new ScatterPlotList()
            {
                Color = color ?? GetNextColor(),
                LineWidth = lineWidth,
                MarkerSize = markerSize,
                Label = label,
                MarkerShape = markerShape,
                LineStyle = lineStyle
            };

            Add(spl);
            return spl;
        }

        /// <summary>
        /// Signal plots have evenly-spaced X points and render very fast.
        /// </summary>
        public SignalPlot AddSignal(double[] ys, double sampleRate = 1, Color? color = null, string label = null)
        {
            SignalPlot signal = new SignalPlot()
            {
                ys = ys,
                sampleRate = sampleRate,
                color = color ?? settings.GetNextColor(),
                label = label,

                // TODO: FIX THIS!!!
                minRenderIndex = 0,
                maxRenderIndex = ys.Length - 1,
            };
            Add(signal);
            return signal;
        }

        /// <summary>
        /// SignalConts plots have evenly-spaced X points and render faster than Signal plots
        /// but data in source arrays cannot be changed after it is loaded.
        /// Methods can be used to update all or portions of the data.
        /// </summary>
        public SignalPlotConst<T> AddSignalConst<T>(T[] ys, double sampleRate = 1, Color? color = null, string label = null) where T : struct, IComparable
        {
            SignalPlotConst<T> plottable = new SignalPlotConst<T>()
            {
                ys = ys,
                sampleRate = sampleRate,
                color = color ?? settings.GetNextColor(),
                label = label,
                minRenderIndex = 0,
                maxRenderIndex = ys.Length - 1,
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Speed-optimized plot for Ys with unevenly-spaced ascending Xs
        /// </summary>
        public SignalPlotXY AddSignalXY(double[] xs, double[] ys, Color? color = null, string label = null)
        {
            SignalPlotXY plottable = new SignalPlotXY()
            {
                xs = xs,
                ys = ys,
                color = color ?? settings.GetNextColor(),
                label = label,
                minRenderIndex = 0,
                maxRenderIndex = ys.Length - 1,
            };
            Add(plottable);
            return plottable;
        }


        /// <summary>
        /// Speed-optimized plot for Ys with unevenly-spaced ascending Xs.
        /// Faster than SignalXY but values cannot be modified after loading.
        /// </summary>
        public SignalPlotXYConst<TX, TY> AddSignalXYConst<TX, TY>(TX[] xs, TY[] ys, Color? color = null, string label = null)
            where TX : struct, IComparable where TY : struct, IComparable
        {
            SignalPlotXYConst<TX, TY> signal = new SignalPlotXYConst<TX, TY>()
            {
                xs = xs,
                ys = ys,
                color = color ?? settings.GetNextColor(),
                label = label,
                minRenderIndex = 0,
                maxRenderIndex = ys.Length - 1,
            };
            Add(signal);
            return signal;
        }

        /// <summary>
        /// Display text at specific X/Y coordinates
        /// </summary>
        public Text AddText(string label, double x, double y, float size = 12, Color? color = null) =>
            AddText(label, x, y, new Drawing.Font() { Size = size, Color = color ?? GetNextColor() });

        /// <summary>
        /// Display text at specific X/Y coordinates
        /// </summary>
        public Text AddText(string label, double x, double y, Drawing.Font font)
        {
            var plottable = new Text()
            {
                text = label,
                x = x,
                y = y,
                Font = font
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a 2D vector field to the plot
        /// </summary>
        public VectorField AddVectorField(
            Vector2[,] vectors,
            double[] xs,
            double[] ys,
            string label = null,
            Color? color = null,
            Drawing.Colormap colormap = null,
            double scaleFactor = 1
            )
        {
            // TODO: refactor constructor to eliminate styling arguments
            var vectorField = new VectorField(vectors, xs, ys,
                colormap, scaleFactor, color ?? settings.GetNextColor())
            { label = label };

            Add(vectorField);
            return vectorField;
        }

        /// <summary>
        /// Add a vertical axis line at a specific Y position
        /// </summary>
        public VLine AddVerticalLine(double x, Color? color = null, float width = 1, LineStyle style = LineStyle.Solid, string label = null)
        {
            VLine plottable = new VLine()
            {
                X = x,
                Color = color ?? settings.GetNextColor(),
                LineWidth = width,
                LineStyle = style,
                Label = label
            };
            Add(plottable);
            return plottable;
        }

        /// <summary>
        /// Add a horizontal span (shades the region between two X positions)
        /// </summary>
        public VSpan AddVerticalSpan(double yMin, double yMax, Color? color = null, string label = null)
        {
            var plottable = new VSpan()
            {
                position1 = yMin,
                position2 = yMax,
                color = color ?? GetNextColor(.5),
                label = label,
            };
            Add(plottable);
            return plottable;
        }
    }
}