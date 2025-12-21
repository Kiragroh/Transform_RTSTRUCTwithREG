using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

[assembly: AssemblyVersion("1.0.0.1")]

namespace VMS.TPS
{
  public class Script
  {
    // ======= UI fields =======
    private StructureSet _ss;
    private Structure _s1;
    private Structure _s2;

    private ComboBox cbS1;
    private ComboBox cbS2;

    private TextBlock txtV1;
    private TextBlock txtV2;

    private TextBlock txtOverlap;
    private TextBlock txtDice;
    private TextBlock txtShortest;
    private TextBlock txtCentroid;

    // Sampling grid step size (mm)
    private const double STEP_X_MM = 2.0;
    private const double STEP_Y_MM = 2.0;
    private const double STEP_Z_MM = 2.0;

    // Colors
    static readonly Brush BgWindow  = new SolidColorBrush(Color.FromRgb(237, 243, 255)); // #EDF3FF
    static readonly Brush BgSection = new SolidColorBrush(Color.FromRgb(246, 250, 255)); // #F6FAFF
    static readonly Brush BrSection = new SolidColorBrush(Color.FromRgb(99, 125, 187));  // #637DBB

    public Script() { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Execute(ScriptContext context, Window window/*, ScriptEnvironment env*/)
    {
      if (context == null || context.StructureSet == null)
        throw new ApplicationException("Bitte Patient und StructureSet öffnen.");

      _ss = context.StructureSet;

      // --- Window setup ---
      window.Title = "Structure Comparison (Volumes · Overlap · Distances · Dice)";
      window.Width = 660;
      window.Height = 440;
      window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
      window.Background = BgWindow;

      var root = new Grid { Margin = new Thickness(14) };
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

      // ===== Section: Structure 1 =====
      GroupBox g1 = Section("Structure 1");
      Grid g1Grid = (Grid)g1.Content;
      cbS1 = new ComboBox { MinWidth = 200, Margin = new Thickness(8, 0, 8, 0) };
      txtV1 = RightLabel();
      FillStructures(cbS1);
      AddRow(g1Grid, new Label { Content = "Struktur:" }, cbS1, new Label { Content = "Volumen (cc):" }, txtV1);
      Grid.SetRow(g1, 0);
      root.Children.Add(g1);

      // ===== Section: Structure 2 =====
      GroupBox g2 = Section("Structure 2");
      Grid g2Grid = (Grid)g2.Content;
      cbS2 = new ComboBox { MinWidth = 200, Margin = new Thickness(8, 0, 8, 0) };
      txtV2 = RightLabel();
      FillStructures(cbS2);
      AddRow(g2Grid, new Label { Content = "Struktur:" }, cbS2, new Label { Content = "Volumen (cc):" }, txtV2);
      Grid.SetRow(g2, 1);
      root.Children.Add(g2);

      // ===== Section: Overlap & Dice =====
      GroupBox gO = Section("Overlap & Ähnlichkeit");
      Grid gOGrid = (Grid)gO.Content;
      txtOverlap = WideValueLabel();   // "0.123 cc (45.6%)"
      txtDice    = WideValueLabel();   // "0.842"
      AddRowSingle(gOGrid, new Label { Content = "Overlap (rel. zu Struktur 2):" }, txtOverlap);
      AddRowSingle(gOGrid, new Label { Content = "Dice-Koeffizient:" }, txtDice);
      Grid.SetRow(gO, 2);
      root.Children.Add(gO);

      // ===== Section: distances =====
      GroupBox gD = Section("Abstände");
      Grid gDGrid = (Grid)gD.Content;
      txtShortest = WideValueLabel();  // "0.32 cm"
      txtCentroid = WideValueLabel();  // "1.24 cm"
      AddRowSingle(gDGrid, new Label { Content = "Kürzester Oberflächenabstand:" }, txtShortest);
      AddRowSingle(gDGrid, new Label { Content = "Centroid–Centroid Abstand (CenterPoint):" }, txtCentroid);
      Grid.SetRow(gD, 3);
      root.Children.Add(gD);

      // Note
      var info = new TextBlock
      {
        Text = "Ergebnisse aktualisieren sich automatisch, sobald beide Dropdowns gesetzt sind.",
        Margin = new Thickness(6),
        Foreground = Brushes.DimGray
      };
      Grid.SetRow(info, 4);
      root.Children.Add(info);

      // Events
      cbS1.SelectionChanged += (s, e) => { _s1 = SelectedStructure(cbS1); UpdateAll(); };
      cbS2.SelectionChanged += (s, e) => { _s2 = SelectedStructure(cbS2); UpdateAll(); };

      window.Content = root;

      // Default selection
      if (cbS1.Items.Count > 0) cbS1.SelectedIndex = 0;
      if (cbS2.Items.Count > 1) cbS2.SelectedIndex = 1;
    }

    // =================== UI helpers ===================
    private static GroupBox Section(string title)
    {
      var gb = new GroupBox
      {
        Header = title,
        Margin = new Thickness(0, 0, 0, 10),
        Padding = new Thickness(8),
        BorderThickness = new Thickness(1),
        BorderBrush = BrSection,
        Background = BgSection
      };

      var grid = new Grid();
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });

      gb.Content = grid;
      return gb;
    }

    private static void AddRow(Grid grid, UIElement l1, UIElement v1, UIElement l2, UIElement v2)
    {
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      int row = grid.RowDefinitions.Count - 1;
      Grid.SetRow(l1, row); Grid.SetColumn(l1, 0); grid.Children.Add(l1);
      Grid.SetRow(v1, row); Grid.SetColumn(v1, 1); grid.Children.Add(v1);
      Grid.SetRow(l2, row); Grid.SetColumn(l2, 2); grid.Children.Add(l2);
      Grid.SetRow(v2, row); Grid.SetColumn(v2, 3); grid.Children.Add(v2);
    }

    private static void AddRowSingle(Grid grid, UIElement l1, UIElement v1)
    {
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      int row = grid.RowDefinitions.Count - 1;
      Grid.SetRow(l1, row); Grid.SetColumn(l1, 0); grid.Children.Add(l1);
      Grid.SetRow(v1, row); Grid.SetColumn(v1, 1); Grid.SetColumnSpan(v1, 3); grid.Children.Add(v1);
    }

    private static TextBlock RightLabel()
    {
      return new TextBlock { Text = "—", Margin = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Right };
    }

    private static TextBlock WideValueLabel()
    {
      return new TextBlock { Text = "—", Margin = new Thickness(6), FontWeight = FontWeights.DemiBold };
    }

    private void FillStructures(ComboBox cb)
    {
      var items = _ss.Structures
                     .Where(s => s != null && !s.IsEmpty && s.HasSegment && s.Volume > 0 && s.DicomType != "SUPPORT")
                     .OrderBy(s => s.Id)
                     .Select(s => s.Id)
                     .ToList();
      foreach (var id in items) cb.Items.Add(id);
    }

    private Structure SelectedStructure(ComboBox cb)
    {
      var id = cb.SelectedItem as string;
      if (string.IsNullOrWhiteSpace(id)) return null;
      return _ss.Structures.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateAll()
    {
      txtV1.Text = _s1 != null ? _s1.Volume.ToString("0.###") : "—";
      txtV2.Text = _s2 != null ? _s2.Volume.ToString("0.###") : "—";

      if (_s1 == null || _s2 == null)
      {
        txtOverlap.Text = "—";
        txtDice.Text = "—";
        txtShortest.Text = "—";
        txtCentroid.Text = "—";
        return;
      }

      double overlapCc = VolumeOverlapCc(_s1, _s2);
      double overlapPctOfS2 = PercentOverlapOfSecond(_s2, overlapCc);
      txtOverlap.Text = String.Format("{0:0.###} cc ({1:0.0}%)", overlapCc, overlapPctOfS2);

      double dice = DiceCoefficient(_s1, _s2);
      txtDice.Text = double.IsNaN(dice) ? "—" : dice.ToString("0.###");

      double dshort = ShortestSurfaceDistanceCm(_s1, _s2);
      txtShortest.Text = double.IsNaN(dshort) ? "—" : String.Format("{0:0.###} cm", dshort);

      double dcentroid = CentroidDistanceCm(_s1, _s2);
      txtCentroid.Text = double.IsNaN(dcentroid) ? "—" : String.Format("{0:0.###} cm", dcentroid);
    }

    // =================== Geometry & metrics ===================

    private static double FloorToStep(double value, double step)
    {
      return Math.Floor(value / step) * step;
    }

    /// <summary> Intersection volume (cm³) estimated via point sampling within the shared bounding-box region. </summary>
    private static double VolumeOverlapCc(Structure s1, Structure s2)
    {
      if (s1 == null || s2 == null) return 0.0;

      Rect3D b1 = s1.MeshGeometry.Bounds;
      Rect3D b2 = s2.MeshGeometry.Bounds;
      Rect3D u  = Rect3D.Union(b1, b2);

      if (b1.Contains(b2)) return Math.Min(s2.Volume, s1.Volume);
      if (b2.Contains(b1)) return Math.Min(s1.Volume, s2.Volume);

      int inside = 0;
      VVector p = new VVector();

      for (double z = FloorToStep(u.Z - STEP_Z_MM, STEP_Z_MM); z < u.Z + u.SizeZ + STEP_Z_MM; z += STEP_Z_MM)
      {
        for (double y = FloorToStep(u.Y - STEP_Y_MM, STEP_Y_MM); y < u.Y + u.SizeY + STEP_Y_MM; y += STEP_Y_MM)
        {
          for (double x = FloorToStep(u.X - STEP_X_MM, STEP_X_MM); x < u.X + u.SizeX + STEP_X_MM; x += STEP_X_MM)
          {
            p.x = x; p.y = y; p.z = z;
            if (s1.IsPointInsideSegment(p) && s2.IsPointInsideSegment(p))
              inside++;
          }
        }
      }

      double voxelMm3 = STEP_X_MM * STEP_Y_MM * STEP_Z_MM; // mm³
      return inside * (voxelMm3 / 1000.0);                 // -> cm³
    }

    private static double PercentOverlapOfSecond(Structure second, double overlapCc)
    {
      if (second == null || second.Volume <= 0) return 0.0;
      double p = (overlapCc / second.Volume) * 100.0;
      if (p < 0) p = 0;
      if (p > 100) p = 100;
      return p;
    }

    /// <summary> Shortest point-to-point distance between mesh surfaces (cm). </summary>
    private static double ShortestSurfaceDistanceCm(Structure s1, Structure s2)
    {
      if (s1 == null || s2 == null) return double.NaN;
      if (Object.ReferenceEquals(s1, s2)) return 0.0;

      Rect3D b1 = s1.MeshGeometry.Bounds;
      Rect3D b2 = s2.MeshGeometry.Bounds;
      if (b1.Contains(b2) || b2.Contains(b1)) return 0.0;

      var v1 = s1.MeshGeometry.Positions;
      var v2 = s2.MeshGeometry.Positions;
      if (v1 == null || v1.Count == 0 || v2 == null || v2.Count == 0) return double.NaN;

      double dminCm = double.MaxValue;
      foreach (Point3D a in v1)
      {
        double ax = a.X, ay = a.Y, az = a.Z; // mm
        foreach (Point3D b in v2)
        {
          double dx = b.X - ax;
          double dy = b.Y - ay;
          double dz = b.Z - az;
          double dcm = Math.Sqrt(dx * dx + dy * dy + dz * dz) / 10.0; // mm -> cm
          if (dcm < dminCm) dminCm = dcm;
        }
      }
      return dminCm;
    }

    /// <summary> Distance between center points (mm -> cm). </summary>
    private static double CentroidDistanceCm(Structure s1, Structure s2)
    {
      if (s1 == null || s2 == null) return double.NaN;

      VVector c1 = s1.CenterPoint; // mm
      VVector c2 = s2.CenterPoint; // mm

      double dx = c2.x - c1.x;
      double dy = c2.y - c1.y;
      double dz = c2.z - c1.z;

      double dmm = Math.Sqrt(dx * dx + dy * dy + dz * dz);
      return dmm / 10.0; // -> cm
    }

    /// <summary> Dice coefficient (0..1) estimated via grid sampling counts. </summary>
    private static double DiceCoefficient(Structure s1, Structure s2)
    {
      if (s1 == null || s2 == null) return double.NaN;
      if (Object.ReferenceEquals(s1, s2)) return 1.0;

      Rect3D b1 = s1.MeshGeometry.Bounds;
      Rect3D b2 = s2.MeshGeometry.Bounds;
      Rect3D u  = Rect3D.Union(b1, b2);

      // Fast-path cases for full containment
      if (b1.Contains(b2)) return Math.Round((2.0 * s2.Volume) / (s1.Volume + s2.Volume), 3);
      if (b2.Contains(b1)) return Math.Round((2.0 * s1.Volume) / (s1.Volume + s2.Volume), 3);

      int nInt = 0, n1 = 0, n2 = 0;
      VVector p = new VVector();

      for (double z = FloorToStep(u.Z - STEP_Z_MM, STEP_Z_MM); z < u.Z + u.SizeZ + STEP_Z_MM; z += STEP_Z_MM)
      {
        for (double y = FloorToStep(u.Y - STEP_Y_MM, STEP_Y_MM); y < u.Y + u.SizeY + STEP_Y_MM; y += STEP_Y_MM)
        {
          for (double x = FloorToStep(u.X - STEP_X_MM, STEP_X_MM); x < u.X + u.SizeX + STEP_X_MM; x += STEP_X_MM)
          {
            p.x = x; p.y = y; p.z = z;
            bool in1 = s1.IsPointInsideSegment(p);
            bool in2 = s2.IsPointInsideSegment(p);
            if (in1) n1++;
            if (in2) n2++;
            if (in1 && in2) nInt++;
          }
        }
      }

      double dice = (2.0 * nInt) / (n1 + n2 + 1e-12);
      return Math.Round(dice, 3);
    }
  }
}
