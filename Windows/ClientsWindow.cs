using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Security_agent
{
    // Показывает список клиентов и открывает их карточки.
    public partial class ClientsWindow : Window
    {
        private readonly AgencyData data;
        private readonly Storage storage;
        private readonly string dataPath;


        // Подключает разметку окна и подготавливает его к работе.
        public ClientsWindow(AgencyData data, Storage storage, string dataPath)
        {
            this.data = data;
            this.storage = storage;
            this.dataPath = dataPath;

            InitializeComponent();
            RefreshClients();
        }

        // Строка таблицы со ссылкой на исходного клиента
        private sealed record ClientRow(Client Client, string Name, string Type, string Address);

        // Обновляет и сортирует таблицу клиентов по имени или названию.
        private void RefreshClients(Client? selected = null)
        {
            var rows = data.Clients.Select(client =>
            {
                if (client is ClientLegalEntity legal)
                    return new ClientRow(client, legal.NameFirm, "Юрлицо", client.Address);
                var person = (ClientIndividual)client;
                string name = $"{person.LastName} {person.FirstName} {person.MiddleName}".Trim();
                return new ClientRow(client, name, "Физлицо", client.Address);
            }).OrderBy(row => row.Name).ToList();

            ClientsGrid.ItemsSource = rows;
            ClientsGrid.SelectedItem = rows.FirstOrDefault(row => row.Client == selected);
        }

        // Открывает пустую карточку клиента.
        private void NewClientClick(object sender, RoutedEventArgs e) => OpenEditor(null);

        // Открывает выбранную карточку для редактирования.
        private void EditClientClick(object sender, RoutedEventArgs e)
        {
            if (ClientsGrid.SelectedItem is ClientRow row)
                OpenEditor(row.Client);
        }

        // Открывает карточку при двойном щелчке по строке.
        private void ClientDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ItemsControl.ContainerFromElement(ClientsGrid, e.OriginalSource as DependencyObject) is DataGridRow)
                EditClientClick(sender, e);
        }

        // Обновляет доступность кнопки редактирования.
        private void ClientSelected(object sender, SelectionChangedEventArgs e)
        {
            EditClientButton.IsEnabled = ClientsGrid.SelectedItem is ClientRow;
        }

        // Открывает форму клиента и обновляет таблицу после сохранения.
        private void OpenEditor(Client? client)
        {
            var editor = new ClientEditorWindow(data, storage, dataPath, client) { Owner = this };
            if (editor.ShowDialog() == true)
                RefreshClients(editor.SavedClient);
        }
    }
}
