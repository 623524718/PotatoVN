using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GalgameManager.Views;

public sealed partial class PasswordDialog : ContentDialog
{
    public PasswordDialog()
    {
        this.InitializeComponent();
        XamlRoot = App.MainWindow.Content.XamlRoot;
        IsPrimaryButtonEnabled = false;
        DefaultButton = ContentDialogButton.Primary;
    }

    /// <summary>
    /// �û����������
    /// </summary>
    public string? Password
    {
        get;
        internal set;
    }

    /// <summary>
    /// Ҫ���û���ʾ����Ϣ
    /// </summary>
    public string Message
    {
        get => MessageTextBlock.Text;
        set => MessageTextBlock.Text = value;
    }

    /// <summary>
    /// ������ռλ�ı�
    /// </summary>
    public string PasswordBoxPlaceholderText
    {
        get => PasswordBox.PlaceholderText;
        set => PasswordBox.PlaceholderText = value;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Password = PasswordBox.Password;
    }

    private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Password = null;
    }

    private void OnPasswordChanging(PasswordBox sender, PasswordBoxPasswordChangingEventArgs args)
    {
        IsPrimaryButtonEnabled = !string.IsNullOrEmpty(PasswordBox.Password);
    }
}
