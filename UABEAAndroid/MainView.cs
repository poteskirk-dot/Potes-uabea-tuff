using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UABEAAndroid;

public class MainView : UserControl
{
    readonly TextBlock title = new() { Text = "UABEA Android", FontSize = 22, FontWeight = FontWeight.Bold };
    readonly TextBlock status = new() { Text = "No file opened.", TextWrapping = TextWrapping.Wrap };
    readonly ListBox files = new() { SelectionMode = SelectionMode.Single };
    readonly TextBox search = new() { Watermark = "Search assets/files..." };
    readonly Button open = new() { Content = "Open", MinHeight = 48 };
    readonly Button export = new() { Content = "Export", MinHeight = 48, IsEnabled = false };
    readonly Button rename = new() { Content = "Rename", MinHeight = 48, IsEnabled = false };
    readonly Button remove = new() { Content = "Remove", MinHeight = 48, IsEnabled = false };
    readonly Button import = new() { Content = "Import", MinHeight = 48, IsEnabled = false };
    readonly Button save = new() { Content = "Save As", MinHeight = 48, IsEnabled = false };

    AssetsManager? manager;
    BundleFileInstance? bundle;
    AssetsFileInstance? assetsFile;
    string? sourcePath;
    readonly List<Entry> entries = new();

    public MainView()
    {
        Background = Brush.Parse("#111111");
        var root = new DockPanel { Margin = new Thickness(14) };

        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(title);
        header.Children.Add(status);

        var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*"), ColumnSpacing = 6 };
        AddButton(buttons, open, 0);
        AddButton(buttons, export, 1);
        AddButton(buttons, rename, 2);
        AddButton(buttons, remove, 3);
        AddButton(buttons, import, 4);

        var searchPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 8) };
        searchPanel.Children.Add(search);

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Top);
        DockPanel.SetDock(searchPanel, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(buttons);
        root.Children.Add(searchPanel);
        root.Children.Add(files);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        bottom.Children.Add(save);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        Content = root;

        open.Click += async (_, _) => await OpenAsync();
        export.Click += async (_, _) => await ExportAsync();
        rename.Click += async (_, _) => await RenameAsync();
        remove.Click += (_, _) => RemoveSelected();
        import.Click += async (_, _) => await ImportAsync();
        save.Click += async (_, _) => await SaveAsync();
        search.TextChanged += (_, _) => RefreshList();
        files.SelectionChanged += (_, _) => UpdateButtons();
    }

    static void AddButton(Grid g, Control c, int col)
    {
        Grid.SetColumn(c, col);
        g.Children.Add(c);
    }

    async Task OpenAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null) return;

        var picked = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Unity assets/bundle",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Unity files") { Patterns = new[] { "*.*" } }
            }
        });
        var file = picked.FirstOrDefault();
        if (file == null) return;

        try
        {
            sourcePath = await CopyToCacheAsync(file);
            manager = new AssetsManager();

            var classData = Path.Combine(AppContext.BaseDirectory, "classdata.tpk");
            if (File.Exists(classData))
                manager.LoadClassPackage(classData);

            var type = Detect(sourcePath);
            entries.Clear();
            bundle = null;
            assetsFile = null;

            if (type == FileKind.Bundle)
            {
                bundle = manager.LoadBundleFile(sourcePath, false);
                foreach (var d in bundle.file.BlockAndDirInfo.DirectoryInfos)
                    entries.Add(new Entry(d.Name, d.Offset, d.DecompressedSize, (d.Flags & 0x04) != 0));
                status.Text = $"Bundle: {Path.GetFileName(sourcePath)} • {entries.Count} files";
            }
            else if (type == FileKind.Assets)
            {
                assetsFile = manager.LoadAssetsFile(sourcePath, true);
                foreach (var a in assetsFile.file.AssetInfos)
                    entries.Add(new Entry($"PathID {a.PathId} • ClassID {a.TypeId}", 0, a.ByteSize, true));
                status.Text = $"Assets file: {Path.GetFileName(sourcePath)} • {entries.Count} assets";
            }
            else
            {
                status.Text = "That file does not look like a Unity assets file or UnityFS bundle.";
            }

            RefreshList();
        }
        catch (Exception ex)
        {
            status.Text = "Open error: " + ex.Message;
        }
    }

    async Task<string> CopyToCacheAsync(IStorageFile file)
    {
        var path = Path.Combine(Path.GetTempPath(), "uabea_" + Guid.NewGuid().ToString("N"));
        await using var input = await file.OpenReadAsync();
        await using var output = File.Create(path);
        await input.CopyToAsync(output);
        return path;
    }

    enum FileKind { Unknown, Assets, Bundle }

    static FileKind Detect(string path)
    {
        using var fs = File.OpenRead(path);
        using var r = new AssetsFileReader(fs);
        r.BigEndian = true;
        if (r.BaseStream.Length < 0x20) return FileKind.Unknown;
        r.Position = 0;
        var header = r.ReadStringLength(7);
        r.Position = 8;
        var format = r.ReadInt32();
        r.Position = format >= 0x16 ? 0x30 : 0x14;
        var version = "";
        while (r.Position < r.BaseStream.Length)
        {
            var b = r.ReadByte();
            if (b == 0) break;
            version += (char)b;
            if (version.Length > 255) break;
        }
        if (header == "UnityFS") return FileKind.Bundle;
        if (format < 0xFF && version.Length >= 5 && version.All(c => char.IsLetterOrDigit(c) || ".\n-".Contains(c)))
            return FileKind.Assets;
        return FileKind.Unknown;
    }

    void RefreshList()
    {
        var q = search.Text?.Trim() ?? "";
        files.ItemsSource = entries
            .Where(e => string.IsNullOrEmpty(q) || e.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Name)
            .ToList();
        UpdateButtons();
    }

    void UpdateButtons()
    {
        var has = files.SelectedItem != null;
        export.IsEnabled = has && bundle != null;
        rename.IsEnabled = has && bundle != null;
        remove.IsEnabled = has && bundle != null;
        import.IsEnabled = bundle != null;
        save.IsEnabled = bundle != null;
    }

    Entry? SelectedEntry()
    {
        var name = files.SelectedItem?.ToString();
        return entries.FirstOrDefault(x => x.Name == name);
    }

    async Task ExportAsync()
    {
        var e = SelectedEntry();
        if (e == null || bundle == null) return;
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;

        var picked = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export asset",
            SuggestedFileName = Path.GetFileName(e.Name)
        });
        if (picked == null) return;

        try
        {
            await using var output = await picked.OpenWriteAsync();
            using var input = bundle.file.DataReader.BaseStream;
            input.Position = e.Offset;
            await CopyExactlyAsync(input, output, e.Size);
            status.Text = "Exported: " + e.Name;
        }
        catch (Exception ex) { status.Text = "Export error: " + ex.Message; }
    }

    async Task RenameAsync()
    {
        var e = SelectedEntry();
        if (e == null) return;
        var dialog = new Window { Title = "Rename", Width = 360, Height = 190, Background = Brush.Parse("#181818") };
        var box = new TextBox { Text = e.Name, Margin = new Thickness(12) };
        var ok = new Button { Content = "Rename", Margin = new Thickness(12), MinHeight = 45 };
        ok.Click += (_, _) => { e.Name = box.Text?.Trim() ?? e.Name; dialog.Close(); };
        dialog.Content = new StackPanel { Spacing = 8, Children = { box, ok } };
        await dialog.ShowDialog(TopLevel.GetTopLevel(this) as Window);
        RefreshList();
    }

    void RemoveSelected()
    {
        var e = SelectedEntry();
        if (e == null) return;
        e.Removed = true;
        RefreshList();
        status.Text = "Marked for removal: " + e.Name + " • tap Save As to write a new bundle";
    }

    async Task ImportAsync()
    {
        if (bundle == null) return;
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var picked = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import file into bundle",
            AllowMultiple = false
        });
        var file = picked.FirstOrDefault();
        if (file == null) return;

        var local = await CopyToCacheAsync(file);
        var name = Path.GetFileName(local);
        entries.Add(new Entry(name, 0, new FileInfo(local).Length, false) { ImportedPath = local });
        RefreshList();
        status.Text = "Imported file staged: " + name;
    }

    async Task SaveAsync()
    {
        if (bundle == null) return;
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var picked = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save modified Unity bundle",
            SuggestedFileName = Path.GetFileName(sourcePath) + ".modified"
        });
        if (picked == null) return;

        try
        {
            var replacers = new List<BundleReplacer>();
            foreach (var e in entries)
            {
                if (e.Removed) { replacers.Add(new BundleRemover(e.OriginalName)); continue; }
                if (e.ImportedPath != null)
                {
                    var s = File.OpenRead(e.ImportedPath);
                    replacers.Add(new BundleReplacerFromStream(e.OriginalName, e.Name, false, s, 0, -1));
                    continue;
                }
                if (e.Name != e.OriginalName)
                    replacers.Add(new BundleRenamer(e.OriginalName, e.Name));
            }

            await using var output = await picked.OpenWriteAsync();
            using var writer = new AssetsFileWriter(output);
            bundle.file.Write(writer, replacers);
            status.Text = "Saved modified bundle.";
        }
        catch (Exception ex) { status.Text = "Save error: " + ex; }
    }

    static async Task CopyExactlyAsync(Stream input, Stream output, long count)
    {
        var buffer = new byte[1024 * 1024];
        while (count > 0)
        {
            var n = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, count)));
            if (n == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, n));
            count -= n;
        }
    }

    public class Entry
    {
        public string Name { get; set; }
        public string OriginalName { get; }
        public long Offset { get; }
        public long Size { get; }
        public bool Serialized { get; }
        public bool Removed { get; set; }
        public string? ImportedPath { get; set; }

        public Entry(string name, long offset, long size, bool serialized)
        {
            Name = name; OriginalName = name; Offset = offset; Size = size; Serialized = serialized;
        }
        public override string ToString() => Name;
    }
}
