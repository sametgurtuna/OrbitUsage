using System.Windows;
using Microsoft.Web.WebView2.Wpf;

namespace Orbit.Views;

public partial class LoginWindow : Window
{
    private readonly WebView2 _webView;

    public LoginWindow(WebView2 webView)
    {
        InitializeComponent();
        _webView = webView;
        WebViewContainer.Children.Add(_webView);
    }

    /// <summary>Removes the WebView2 control from this window's visual tree before it closes,
    /// so the caller can safely re-parent it elsewhere (a control can only live in one tree).</summary>
    public void ReleaseWebView() => WebViewContainer.Children.Remove(_webView);
}
