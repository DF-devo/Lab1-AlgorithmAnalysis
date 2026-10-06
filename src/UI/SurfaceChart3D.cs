using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

namespace UI;

/// <summary>
/// Построение 3D-сцены поверхности время(N, M) на HelixToolkit:
/// настоящий Z-буфер вместо псевдо-3D (никаких разрывов и неверных перекрытий),
/// орбитальная камера (вращение левой кнопкой, зум колесом), экранные линии и подписи.
/// </summary>
public static class SurfaceChart3D
{
    private const double BoxX = 10;   // ширина ящика по N
    private const double BoxY = 10;   // глубина ящика по M
    private const double BoxZ = 6;    // высота ящика по времени

    private static readonly Color GridColor = Color.FromRgb(44, 59, 88);
    private static readonly Color AxisColor = Color.FromRgb(105, 119, 145);
    private static readonly Color SurfaceLowColor = Color.FromRgb(38, 52, 90);
    private static readonly Color SurfaceHighColor = Color.FromRgb(105, 216, 194);

    public static void Render(
        ModelVisual3D sceneRoot,
        HelixViewport3D viewport,
        IReadOnlyList<MainWindow.GridPoint> grid,
        IReadOnlyList<MainWindow.ComparisonSeries> comparisons,
        int runs)
    {
        sceneRoot.Children.Clear();

        var ns = grid.Select(p => p.N).Distinct().OrderBy(v => v).ToList();
        var ms = grid.Select(p => p.M).Distinct().OrderBy(v => v).ToList();
        var nMin = ns[0];
        var nMax = ns[^1];
        var mMin = ms[0];
        var mMax = ms[^1];
        var nRange = Math.Max(1, nMax - nMin);
        var mRange = Math.Max(1, mMax - mMin);

        // Лог-шкала времени с ограничением ~3,2 декадами: ячейки ниже низа шкалы
        // рисуются на полу, их реальное время видно в тултипе
        var curvesMax = comparisons.Count == 0
            ? 0
            : comparisons.Max(c => c.Points.Count == 0 ? 0 : c.Points.Where(p => p.N >= nMin && p.N <= nMax).Max(p => p.Value));
        var curvesMin = comparisons.Count == 0
            ? double.MaxValue
            : comparisons.Min(c => c.Points.Count == 0 ? double.MaxValue : c.Points.Where(p => p.N >= nMin && p.N <= nMax).Min(p => p.Value));
        var zRawMax = Math.Max(grid.Max(p => p.Value), curvesMax);
        var zRawMin = Math.Min(grid.Min(p => p.Value), curvesMin);
        var zLogMax = Math.Log10(Math.Max(zRawMax, 1e-3));
        var zLogMin = Math.Max(Math.Log10(Math.Max(zRawMin * 0.5, 1e-4)), zLogMax - 3.2);
        if (zLogMax - zLogMin < 0.6)
            zLogMax = zLogMin + 0.6;
        var zLogRange = zLogMax - zLogMin;
        double ZFrac(double z) => Math.Clamp((Math.Log10(Math.Max(z, 1e-4)) - zLogMin) / zLogRange, 0, 1);

        Point3D P(double n, double m, double frac) => new(
            BoxX * (n - nMin) / nRange,
            BoxY * (m - mMin) / mRange,
            BoxZ * frac);

        // —— пол ——
        sceneRoot.Children.Add(MakeQuad(
            P(nMin, mMin, 0), P(nMax, mMin, 0), P(nMax, mMax, 0), P(nMin, mMax, 0),
            Color.FromRgb(17, 24, 42)));

        // —— каркас стенок (n = nMin и m = mMin) и сетка пола ——
        var gridLines = new Point3DCollection();
        foreach (var m in ms)
        {
            AddSegment(gridLines, P(nMin, m, 0), P(nMin, m, 1));   // вертикали стенки n = nMin
            AddSegment(gridLines, P(nMin, m, 0), P(nMax, m, 0));   // линии пола вдоль N
        }
        foreach (var n in ns)
        {
            AddSegment(gridLines, P(n, mMin, 0), P(n, mMin, 1));   // вертикали стенки m = mMin
            AddSegment(gridLines, P(n, mMin, 0), P(n, mMax, 0));   // линии пола вдоль M
        }
        foreach (var t in LogTicks(zLogMin, zLogMax, decadesOnly: true))
        {
            AddSegment(gridLines, P(nMin, mMin, ZFrac(t)), P(nMin, mMax, ZFrac(t)));
            AddSegment(gridLines, P(nMin, mMin, ZFrac(t)), P(nMax, mMin, ZFrac(t)));
        }
        sceneRoot.Children.Add(new LinesVisual3D { Points = gridLines, Color = GridColor, Thickness = 1 });

        // —— рёбра ящика ——
        var edges = new Point3DCollection();
        AddSegment(edges, P(nMin, mMin, 0), P(nMax, mMin, 0));
        AddSegment(edges, P(nMin, mMin, 0), P(nMin, mMax, 0));
        AddSegment(edges, P(nMin, mMax, 0), P(nMax, mMax, 0));
        AddSegment(edges, P(nMax, mMin, 0), P(nMax, mMax, 0));
        AddSegment(edges, P(nMin, mMax, 0), P(nMin, mMax, 1));
        AddSegment(edges, P(nMin, mMin, 0), P(nMin, mMin, 1));
        AddSegment(edges, P(nMax, mMin, 0), P(nMax, mMin, 1));
        AddSegment(edges, P(nMin, mMin, 1), P(nMin, mMax, 1));
        AddSegment(edges, P(nMin, mMin, 1), P(nMax, mMin, 1));
        sceneRoot.Children.Add(new LinesVisual3D { Points = edges, Color = AxisColor, Thickness = 1.6 });

        // —— поверхность: по одной модели на ячейку (тултип на ячейку), настоящий Z-буфер ——
        var lookup = grid.ToDictionary(p => (p.N, p.M), p => p.Value);
        double Z(int n, int m) => lookup.TryGetValue((n, m), out var v) ? v : 0;
        for (var i = 0; i < ns.Count - 1; i++)
        {
            for (var j = 0; j < ms.Count - 1; j++)
            {
                var n0 = ns[i];
                var n1 = ns[i + 1];
                var m0 = ms[j];
                var m1 = ms[j + 1];
                var frac = ZFrac(Z(n1, m1));
                var color = LerpColor(SurfaceLowColor, SurfaceHighColor, frac);
                var material = new DiffuseMaterial(new SolidColorBrush(color));
                var quadModel = new GeometryModel3D
                {
                    Geometry = QuadMesh(
                        P(n0, m0, ZFrac(Z(n0, m0))),
                        P(n1, m0, ZFrac(Z(n1, m0))),
                        P(n1, m1, ZFrac(Z(n1, m1))),
                        P(n0, m1, ZFrac(Z(n0, m1)))),
                    Material = material,
                    BackMaterial = material
                };
                var quadHolder = new ModelVisual3D { Content = quadModel };
                ToolTipService.SetToolTip(quadHolder,
                    $"N = {n0}–{n1}, M = {m0}–{m1}{Environment.NewLine}Время: {Z(n1, m1):0.####} мс");
                sceneRoot.Children.Add(quadHolder);
            }
        }

        // каркас поверхности поверх заливки
        var surfaceWire = new Point3DCollection();
        for (var i = 0; i < ns.Count; i++)
            for (var j = 0; j < ms.Count - 1; j++)
                AddSegment(surfaceWire, P(ns[i], ms[j], ZFrac(Z(ns[i], ms[j]))), P(ns[i], ms[j + 1], ZFrac(Z(ns[i], ms[j + 1]))));
        for (var j = 0; j < ms.Count; j++)
            for (var i = 0; i < ns.Count - 1; i++)
                AddSegment(surfaceWire, P(ns[i], ms[j], ZFrac(Z(ns[i], ms[j]))), P(ns[i + 1], ms[j], ZFrac(Z(ns[i + 1], ms[j]))));
        sceneRoot.Children.Add(new LinesVisual3D { Points = surfaceWire, Color = Color.FromRgb(11, 16, 32), Thickness = 1 });

        // —— кривые сравнения: матричные — по диагонали M = N, остальные — по срезу M = M_max ——
        foreach (var series in comparisons)
        {
            var onDiagonal = series.Algorithm.SupportsMatrixDimensions;
            double CurveM(double n) => onDiagonal ? n : mMax;
            var curvePoints = new Point3DCollection();
            var pts = series.Points
                .Where(p => p.N >= nMin && p.N <= nMax)
                .Select(p => P(p.N, CurveM(p.N), ZFrac(p.Value)))
                .ToList();
            // LinesVisual3D интерпретирует точки как пары «от–до»: дублируем промежуточные
            for (var i = 0; i < pts.Count - 1; i++)
            {
                curvePoints.Add(pts[i]);
                curvePoints.Add(pts[i + 1]);
            }
            if (curvePoints.Count > 0)
                sceneRoot.Children.Add(new LinesVisual3D
                {
                    Points = curvePoints,
                    Color = ((SolidColorBrush)series.Brush).Color,
                    Thickness = 3
                });

            foreach (var point in series.Points.Where(p => p.N >= nMin && p.N <= nMax))
            {
                var sphere = new SphereVisual3D
                {
                    Center = P(point.N, CurveM(point.N), ZFrac(point.Value)),
                    Radius = 0.09,
                    Material = new DiffuseMaterial(series.Brush)
                };
                var holder = new ModelVisual3D();
                holder.Children.Add(sphere);
                ToolTipService.SetToolTip(holder,
                    $"{series.Algorithm.Name}{Environment.NewLine}N = {point.N:N0}{(onDiagonal ? ", M = N" : string.Empty)}{Environment.NewLine}{point.Value:0.####} мс");
                sceneRoot.Children.Add(holder);
            }
        }

        // —— подписи осей (билборды, всегда развёрнуты к камере) ——
        foreach (var t in LogTicks(zLogMin, zLogMax, decadesOnly: false))
        {
            sceneRoot.Children.Add(new BillboardTextVisual3D
            {
                Position = new Point3D(-0.4, BoxY, BoxZ * ZFrac(t)),
                Text = t < 1 ? t.ToString("0.##", CultureInfo.CurrentCulture) : t.ToString("N0", CultureInfo.CurrentCulture),
                Foreground = new SolidColorBrush(Color.FromRgb(201, 211, 232))
            });
        }
        foreach (var n in SparseTicks(ns))
        {
            sceneRoot.Children.Add(new BillboardTextVisual3D
            {
                Position = new Point3D(BoxX * (n - nMin) / nRange, BoxY + 0.3, 0),
                Text = n.ToString("0"),
                Foreground = new SolidColorBrush(Color.FromRgb(201, 211, 232))
            });
        }
        foreach (var m in SparseTicks(ms))
        {
            sceneRoot.Children.Add(new BillboardTextVisual3D
            {
                Position = new Point3D(BoxX + 0.6, BoxY * (m - mMin) / mRange, 0),
                Text = m.ToString("0"),
                Foreground = new SolidColorBrush(Color.FromRgb(201, 211, 232))
            });
        }
        sceneRoot.Children.Add(new BillboardTextVisual3D
        {
            Position = new Point3D(BoxX / 2, BoxY + 0.9, 0),
            Text = "N",
            Foreground = new SolidColorBrush(Color.FromRgb(201, 211, 232))
        });
        sceneRoot.Children.Add(new BillboardTextVisual3D
        {
            Position = new Point3D(BoxX + 0.9, BoxY / 2, 0),
            Text = "M",
            Foreground = new SolidColorBrush(Color.FromRgb(201, 211, 232))
        });
        sceneRoot.Children.Add(new BillboardTextVisual3D
        {
            Position = new Point3D(-0.4, BoxY, BoxZ + 0.4),
            Text = "мс, лог",
            Foreground = new SolidColorBrush(Color.FromRgb(137, 149, 173))
        });

        // —— камера: классический ракурс на центр ящика; вращение мышью доступно всегда ——
        viewport.ShowCoordinateSystem = false;
        viewport.Camera = new OrthographicCamera
        {
            Position = new Point3D(BoxX / 2 + 21.5, BoxY / 2 - 19.5, BoxZ / 2 + 15.5),
            LookDirection = new Vector3D(-21.5, 19.5, -15.5),
            UpDirection = new Vector3D(0, 0, 1),
            Width = 17
        };
    }

    private static void AddSegment(Point3DCollection collection, Point3D a, Point3D b)
    {
        collection.Add(a);
        collection.Add(b);
    }

    private static IEnumerable<double> LogTicks(double zLogMin, double zLogMax, bool decadesOnly)
    {
        double[] candidates = [0.05, 0.1, 0.5, 1, 5, 10, 50, 100, 500, 1000, 5000, 10000, 50000, 100000];
        var ticks = candidates.Where(t => Math.Log10(t) >= zLogMin && Math.Log10(t) <= zLogMax);
        if (decadesOnly && zLogMax - zLogMin > 1.5)
            ticks = ticks.Where(t =>
            {
                var mantissa = t / Math.Pow(10, Math.Floor(Math.Log10(t)));
                return Math.Abs(mantissa - 1) < 1e-9;
            });
        return ticks;
    }

    private static IEnumerable<int> SparseTicks(IReadOnlyList<int> values)
    {
        if (values.Count <= 6)
            return values;
        var step = (int)Math.Ceiling(values.Count / 5.0);
        return values.Where((_, i) => i % step == 0 || i == values.Count - 1).Distinct();
    }

    private static MeshGeometry3D QuadMesh(Point3D a, Point3D b, Point3D c, Point3D d)
    {
        var mesh = new MeshGeometry3D();
        mesh.Positions.Add(a);
        mesh.Positions.Add(b);
        mesh.Positions.Add(c);
        mesh.Positions.Add(a);
        mesh.Positions.Add(c);
        mesh.Positions.Add(d);
        return mesh;
    }

    private static ModelVisual3D MakeQuad(Point3D a, Point3D b, Point3D c, Point3D d, Color color)
    {
        var model = new GeometryModel3D
        {
            Geometry = QuadMesh(a, b, c, d),
            Material = new DiffuseMaterial(new SolidColorBrush(color)),
            BackMaterial = new DiffuseMaterial(new SolidColorBrush(color))
        };
        return new ModelVisual3D { Content = model };
    }

    private static void AddSegment2(Point3DCollection collection, Point3D a, Point3D b)
    {
        collection.Add(a);
        collection.Add(b);
    }

    private static Color LerpColor(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t));
    }
}
