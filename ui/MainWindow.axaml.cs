using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using TextBridge.Core;

namespace TextBridge;

public partial class MainWindow : Window
{
    private Settings cfg;
    private Extension? ext;
    private ObservableCollection<logentry> logs = new();

    public MainWindow(Extension extension, Settings config)
    {
        ext = extension;
        cfg = config;

        ext.logevent += msg =>
        {
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => addlog(msg));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"log error: {ex.Message}");
            }
        };

        InitializeComponent();
        initcontrols();
        setuphandlers();

        if (cfg.provider == "LocalLLM")
        {
            Task.Run(async () => await fetchlocalmodels());
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void initcontrols()
    {
        var pinbutton = this.FindControl<Button>("pinbutton");
        var pinpath = this.FindControl<Avalonia.Controls.Shapes.Path>("pinpath");
        var keybox = this.FindControl<TextBox>("keybox");
        var eyebutton = this.FindControl<Button>("eyebutton");
        var apikeylabel = this.FindControl<TextBlock>("apikeylabel");
        var providerdrop = this.FindControl<ComboBox>("providerdrop");
        var modeldrop = this.FindControl<ComboBox>("modeldrop");
        var refreshmodels = this.FindControl<Button>("refreshmodels");
        var localllmpanel = this.FindControl<StackPanel>("localllmpanel");
        var localipbox = this.FindControl<TextBox>("localipbox");
        var localportbox = this.FindControl<TextBox>("localportbox");
        var intargetdrop = this.FindControl<ComboBox>("intargetdrop");
        var outtargetdrop = this.FindControl<ComboBox>("outtargetdrop");
        var incomingcheck = this.FindControl<CheckBox>("incomingcheck");
        var outgoingcheck = this.FindControl<CheckBox>("outgoingcheck");
        var chatcheck = this.FindControl<CheckBox>("chatcheck");
        var shoutcheck = this.FindControl<CheckBox>("shoutcheck");
        var whispercheck = this.FindControl<CheckBox>("whispercheck");
        var custominstructionsbox = this.FindControl<TextBox>("custominstructionsbox");
        var loglist = this.FindControl<ItemsControl>("loglist");

        if (pinbutton != null && pinpath != null)
        {
            pinbutton.Click += (s, e) =>
            {
                this.Topmost = !this.Topmost;
                pinpath.Fill = this.Topmost ? Avalonia.Media.Brushes.White : Avalonia.Media.Brushes.Gray;
            };
        }

        if (providerdrop != null)
        {
            foreach (var provider in Settings.providermodels.Keys)
            {
                providerdrop.Items.Add(provider);
            }
            providerdrop.SelectedItem = cfg.provider;
        }

        if (modeldrop != null && Settings.providermodels.ContainsKey(cfg.provider))
        {
            foreach (var model in Settings.providermodels[cfg.provider])
            {
                modeldrop.Items.Add(model);
            }
            modeldrop.SelectedItem = cfg.model;
        }

        if (localllmpanel != null)
        {
            localllmpanel.IsVisible = cfg.provider == "LocalLLM";
        }

        if (refreshmodels != null)
        {
            refreshmodels.IsVisible = cfg.provider == "LocalLLM" || cfg.provider == "FantasyAI";
        }

        if (keybox != null && eyebutton != null && apikeylabel != null)
        {
            var islocalllm = cfg.provider == "LocalLLM";
            keybox.IsEnabled = !islocalllm;
            eyebutton.IsEnabled = !islocalllm;
            apikeylabel.Opacity = islocalllm ? 0.5 : 1.0;
        }

        if (localipbox != null)
        {
            localipbox.Text = cfg.localip;
        }

        if (localportbox != null)
        {
            localportbox.Text = cfg.localport;
        }

        if (intargetdrop != null)
        {
            foreach (var lang in Settings.languages)
            {
                intargetdrop.Items.Add(lang);
            }
            intargetdrop.SelectedItem = cfg.intarget;
            intargetdrop.IsEnabled = cfg.enableincoming;
        }

        if (outtargetdrop != null)
        {
            foreach (var lang in Settings.languages)
            {
                outtargetdrop.Items.Add(lang);
            }
            outtargetdrop.SelectedItem = cfg.outtarget;
            outtargetdrop.IsEnabled = cfg.enableoutgoing;
        }

        if (keybox != null) keybox.Text = cfg.getkey();
        if (incomingcheck != null) incomingcheck.IsChecked = cfg.enableincoming;
        if (outgoingcheck != null) outgoingcheck.IsChecked = cfg.enableoutgoing;
        if (chatcheck != null) chatcheck.IsChecked = cfg.dochat;
        if (shoutcheck != null) shoutcheck.IsChecked = cfg.doshout;
        if (whispercheck != null) whispercheck.IsChecked = cfg.dowhisper;
        if (custominstructionsbox != null) custominstructionsbox.Text = cfg.custominstructions;

        if (loglist != null)
        {
            loglist.ItemsSource = logs;
            loglist.ItemTemplate = new FuncDataTemplate<logentry>((entry, scope) =>
            {
                if (entry == null)
                    return new TextBlock { Text = "" };

                var mainpanel = new StackPanel { Margin = new Avalonia.Thickness(5, 3, 5, 3) };
                var msg = entry.message ?? "";

                if (msg.StartsWith("← ") || msg.StartsWith("→ "))
                {
                    var direction = msg.Substring(0, 1);
                    var rest = msg.Substring(2);
                    var parts = rest.Split(new[] { " | ", " → " }, StringSplitOptions.None);

                    if (parts.Length >= 3)
                    {
                        var original = parts[1];
                        var translated = parts[2];

                        var timepanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
                        timepanel.Children.Add(new TextBlock { Text = entry.time ?? "", Foreground = Avalonia.Media.Brushes.Gray, FontSize = 9 });
                        mainpanel.Children.Add(timepanel);

                        var dircolor = direction == "←" ? "#FF6B6B" : "#4A9EFF";

                        var contentgrid = new Grid { Margin = new Avalonia.Thickness(0, 2, 0, 0), MinHeight = 30 };
                        contentgrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Pixel) });
                        contentgrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        contentgrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        contentgrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        var arrowtext = new TextBlock { Text = direction, Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(dircolor)), FontSize = 16, FontWeight = Avalonia.Media.FontWeight.Bold };
                        Grid.SetColumn(arrowtext, 0);
                        Grid.SetRow(arrowtext, 0);
                        Grid.SetRowSpan(arrowtext, 2);
                        contentgrid.Children.Add(arrowtext);

                        var originaltext = new TextBlock { Text = original, Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#999999")), FontSize = 10, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 0, 0, 2) };
                        Grid.SetColumn(originaltext, 1);
                        Grid.SetRow(originaltext, 0);
                        contentgrid.Children.Add(originaltext);

                        var translatedtext = new TextBlock { Text = translated, Foreground = Avalonia.Media.Brushes.White, FontSize = 11, FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
                        Grid.SetColumn(translatedtext, 1);
                        Grid.SetRow(translatedtext, 1);
                        contentgrid.Children.Add(translatedtext);

                        mainpanel.Children.Add(contentgrid);
                    }
                    else
                    {
                        var timepanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
                        timepanel.Children.Add(new TextBlock { Text = entry.time ?? "", Foreground = Avalonia.Media.Brushes.Gray, FontSize = 9 });
                        mainpanel.Children.Add(timepanel);
                        mainpanel.Children.Add(new TextBlock { Text = msg, Foreground = Avalonia.Media.Brushes.White, FontSize = 10, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 2, 0, 0) });
                    }
                }
                else
                {
                    var toppanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
                    toppanel.Children.Add(new TextBlock { Text = entry.time ?? "", Foreground = Avalonia.Media.Brushes.Gray, FontSize = 9 });
                    mainpanel.Children.Add(toppanel);
                    mainpanel.Children.Add(new TextBlock { Text = msg, Foreground = Avalonia.Media.Brushes.White, FontSize = 10, TextWrapping = Avalonia.Media.TextWrapping.NoWrap, Margin = new Avalonia.Thickness(0, 2, 0, 0) });
                }

                return mainpanel;
            });
        }
    }

    private void setuphandlers()
    {
        var keybox = this.FindControl<TextBox>("keybox");
        var eyebutton = this.FindControl<Button>("eyebutton");
        var eyepath = this.FindControl<Avalonia.Controls.Shapes.Path>("eyepath");
        var apikeylabel = this.FindControl<TextBlock>("apikeylabel");
        var providerdrop = this.FindControl<ComboBox>("providerdrop");
        var modeldrop = this.FindControl<ComboBox>("modeldrop");
        var refreshmodels = this.FindControl<Button>("refreshmodels");
        var localllmpanel = this.FindControl<StackPanel>("localllmpanel");
        var localipbox = this.FindControl<TextBox>("localipbox");
        var localportbox = this.FindControl<TextBox>("localportbox");
        var intargetdrop = this.FindControl<ComboBox>("intargetdrop");
        var outtargetdrop = this.FindControl<ComboBox>("outtargetdrop");
        var incomingcheck = this.FindControl<CheckBox>("incomingcheck");
        var outgoingcheck = this.FindControl<CheckBox>("outgoingcheck");
        var chatcheck = this.FindControl<CheckBox>("chatcheck");
        var shoutcheck = this.FindControl<CheckBox>("shoutcheck");
        var whispercheck = this.FindControl<CheckBox>("whispercheck");
        var custominstructionsbox = this.FindControl<TextBox>("custominstructionsbox");

        if (eyebutton != null && keybox != null && eyepath != null)
        {
            eyebutton.Click += (s, e) =>
            {
                if (keybox.PasswordChar == '●')
                {
                    keybox.PasswordChar = '\0';
                    eyepath.Data = Avalonia.Media.Geometry.Parse("M12 7c2.76 0 5 2.24 5 5 0 .65-.13 1.26-.36 1.83l2.92 2.92c1.51-1.26 2.7-2.89 3.43-4.75-1.73-4.39-6-7.5-11-7.5-1.4 0-2.74.25-3.98.7l2.16 2.16C10.74 7.13 11.35 7 12 7zM2 4.27l2.28 2.28.46.46C3.08 8.3 1.78 10.02 1 12c1.73 4.39 6 7.5 11 7.5 1.55 0 3.03-.3 4.38-.84l.42.42L19.73 22 21 20.73 3.27 3 2 4.27zM7.53 9.8l1.55 1.55c-.05.21-.08.43-.08.65 0 1.66 1.34 3 3 3 .22 0 .44-.03.65-.08l1.55 1.55c-.67.33-1.41.53-2.2.53-2.76 0-5-2.24-5-5 0-.79.2-1.53.53-2.2zm4.31-.78l3.15 3.15.02-.16c0-1.66-1.34-3-3-3l-.17.01z");
                }
                else
                {
                    keybox.PasswordChar = '●';
                    eyepath.Data = Avalonia.Media.Geometry.Parse("M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z");
                }
            };
        }

        if (keybox != null)
        {
            keybox.TextChanged += (s, e) =>
            {
                cfg.setkey(keybox.Text ?? "");
                autosave();
            };
        }

        if (providerdrop != null)
        {
            providerdrop.SelectionChanged += (s, e) =>
            {
                if (providerdrop.SelectedItem is string selectedprovider)
                {
                    cfg.provider = selectedprovider;
                    if (modeldrop != null)
                    {
                        modeldrop.Items.Clear();
                        if (Settings.providermodels.ContainsKey(selectedprovider))
                        {
                            foreach (var model in Settings.providermodels[selectedprovider])
                            {
                                modeldrop.Items.Add(model);
                            }
                            modeldrop.SelectedIndex = 0;
                            cfg.model = Settings.providermodels[selectedprovider][0];
                        }
                    }
                    if (localllmpanel != null)
                    {
                        localllmpanel.IsVisible = selectedprovider == "LocalLLM";
                    }
                    if (refreshmodels != null)
                    {
                        refreshmodels.IsVisible = selectedprovider == "LocalLLM" || selectedprovider == "FantasyAI";
                    }
                    if (keybox != null && eyebutton != null && apikeylabel != null)
                    {
                        var islocalllm = selectedprovider == "LocalLLM";
                        keybox.IsEnabled = !islocalllm;
                        eyebutton.IsEnabled = !islocalllm;
                        apikeylabel.Opacity = islocalllm ? 0.5 : 1.0;
                        keybox.Text = cfg.getkey();
                    }
                    autosave();
                }
            };
        }

        if (refreshmodels != null && modeldrop != null)
        {
            refreshmodels.Click += async (s, e) =>
            {
                if (cfg.provider == "FantasyAI")
                    await fetchfantasymodels();
                else
                    await fetchlocalmodels();
            };
        }

        if (localipbox != null)
        {
            localipbox.TextChanged += (s, e) =>
            {
                cfg.localip = localipbox.Text ?? "127.0.0.1";
                autosave();
            };
        }

        if (localportbox != null)
        {
            localportbox.TextChanged += (s, e) =>
            {
                cfg.localport = localportbox.Text ?? "11434";
                autosave();
            };
        }

        if (modeldrop != null)
        {
            modeldrop.SelectionChanged += (s, e) =>
            {
                if (modeldrop.SelectedItem is string m)
                {
                    cfg.model = m;
                    autosave();
                }
            };
        }

        if (intargetdrop != null)
        {
            intargetdrop.SelectionChanged += (s, e) =>
            {
                if (intargetdrop.SelectedItem is string tl)
                {
                    cfg.intarget = tl;
                    autosave();
                }
            };
        }

        if (outtargetdrop != null)
        {
            outtargetdrop.SelectionChanged += (s, e) =>
            {
                if (outtargetdrop.SelectedItem is string tl)
                {
                    cfg.outtarget = tl;
                    autosave();
                }
            };
        }

        if (incomingcheck != null && intargetdrop != null)
        {
            incomingcheck.IsCheckedChanged += (s, e) =>
            {
                cfg.enableincoming = incomingcheck.IsChecked ?? false;
                intargetdrop.IsEnabled = cfg.enableincoming;
                autosave();
            };
        }

        if (outgoingcheck != null && outtargetdrop != null)
        {
            outgoingcheck.IsCheckedChanged += (s, e) =>
            {
                cfg.enableoutgoing = outgoingcheck.IsChecked ?? false;
                outtargetdrop.IsEnabled = cfg.enableoutgoing;
                autosave();
            };
        }

        if (chatcheck != null)
        {
            chatcheck.IsCheckedChanged += (s, e) =>
            {
                cfg.dochat = chatcheck.IsChecked ?? false;
                autosave();
            };
        }

        if (shoutcheck != null)
        {
            shoutcheck.IsCheckedChanged += (s, e) =>
            {
                cfg.doshout = shoutcheck.IsChecked ?? false;
                autosave();
            };
        }

        if (whispercheck != null)
        {
            whispercheck.IsCheckedChanged += (s, e) =>
            {
                cfg.dowhisper = whispercheck.IsChecked ?? false;
                autosave();
            };
        }

        if (custominstructionsbox != null)
        {
            custominstructionsbox.TextChanged += (s, e) =>
            {
                cfg.custominstructions = custominstructionsbox.Text ?? "";
                autosave();
            };
        }
    }

    private void autosave()
    {
        cfg.save();
        ext?.updateconfig(cfg);
    }

    private async Task fetchlocalmodels()
    {
        try
        {
            await Task.Delay(500);

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"http://{cfg.localip}:{cfg.localport}/api/tags";
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);

            if (result.TryGetProperty("models", out var models))
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var modeldrop = this.FindControl<ComboBox>("modeldrop");
                    if (modeldrop != null)
                    {
                        var savedmodel = cfg.model;
                        modeldrop.Items.Clear();
                        foreach (var model in models.EnumerateArray())
                        {
                            if (model.TryGetProperty("name", out var name))
                            {
                                modeldrop.Items.Add(name.GetString());
                            }
                        }
                        if (modeldrop.Items.Count > 0)
                        {
                            var matchingmodel = modeldrop.Items.Cast<object>().FirstOrDefault(m => m?.ToString() == savedmodel);
                            if (matchingmodel != null)
                            {
                                modeldrop.SelectedItem = matchingmodel;
                            }
                            else
                            {
                                modeldrop.SelectedIndex = 0;
                                cfg.model = modeldrop.Items[0]?.ToString() ?? "custom-model";
                                autosave();
                            }
                            addlog($"loaded {modeldrop.Items.Count} models from local llm");
                        }
                        else
                        {
                            addlog("no models found on local llm");
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                addlog($"failed to fetch models: {ex.Message}");
            });
        }
    }


    private async Task fetchfantasymodels()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cfg.getkey()}");
            var response = await client.GetAsync("https://fantasyai.cloud/api/v1/models");
            var json = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);

            if (result.TryGetProperty("data", out var models))
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var modeldrop = this.FindControl<ComboBox>("modeldrop");
                    if (modeldrop != null)
                    {
                        var savedmodel = cfg.model;
                        modeldrop.Items.Clear();
                        foreach (var model in models.EnumerateArray())
                        {
                            if (model.TryGetProperty("id", out var id))
                            {
                                modeldrop.Items.Add(id.GetString());
                            }
                        }
                        if (modeldrop.Items.Count > 0)
                        {
                            var matchingmodel = modeldrop.Items.Cast<object>().FirstOrDefault(m => m?.ToString() == savedmodel);
                            if (matchingmodel != null)
                            {
                                modeldrop.SelectedItem = matchingmodel;
                            }
                            else
                            {
                                modeldrop.SelectedIndex = 0;
                                cfg.model = modeldrop.Items[0]?.ToString() ?? "gpt-4o";
                                autosave();
                            }
                            addlog($"loaded {modeldrop.Items.Count} models from fantasyai");
                        }
                        else
                        {
                            addlog("no models found on fantasyai");
                        }
                    }
                });
            }
            else
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    addlog("failed to fetch fantasyai models: check your api key");
                });
            }
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                addlog($"failed to fetch models: {ex.Message}");
            });
        }
    }

    public void addlog(string msg)
    {
        try
        {
            var entry = new logentry
            {
                time = DateTime.Now.ToString("HH:mm:ss"),
                message = msg
            };
            logs.Add(entry);

            if (logs.Count > 200)
            {
                logs.RemoveAt(0);
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var logscroll = this.FindControl<ScrollViewer>("logscroll");
                    if (logscroll != null)
                    {
                        logscroll.ScrollToEnd();
                    }
                }
                catch
                {
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
        catch
        {
        }
    }

    public class logentry
    {
        public string time { get; set; } = "";
        public string message { get; set; } = "";
    }
}
