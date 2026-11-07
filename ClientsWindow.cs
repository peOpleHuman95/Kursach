using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Security_agent
{
    public partial class ClientsWindow : Window
    {
        private readonly AgencyData data;
        private readonly Storage storage;
        private readonly string dataPath;

        private Client? selectedClient;

        public ClientsWindow(AgencyData data, Storage storage, string dataPath)
        {
            this.data = data;
            this.storage = storage;
            this.dataPath = dataPath;

            InitializeComponent();
            RefreshClients();
            ResetForm();
        }

        // Строка таблицы со ссылкой на исходного клиента
        private sealed record ClientRow(Client Client, string Name, string Type, string Address);

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

        private void NewClientClick(object sender, RoutedEventArgs e)
        {
            ResetForm();
        }

        private void ResetForm()
        {
            ClientsGrid.SelectedItem = null;
            selectedClient = null;

            FormTitle.Text = "Новый клиент";
            ClientTypeBox.IsEnabled = true;
            ClientTypeBox.SelectedIndex = 0;

            LastNameBox.Clear();
            FirstNameBox.Clear();
            MiddleNameBox.Clear();
            PassportBox.Clear();
            CompanyBox.Clear();
            AddressBox.Clear();
        }

        private void ClientTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            bool isLegal = ClientTypeBox.SelectedIndex == 1;
            LegalFields.Visibility = isLegal ? Visibility.Visible : Visibility.Collapsed;
            IndividualFields.Visibility = isLegal ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ClientSelected(object sender, SelectionChangedEventArgs e)
        {
            if (ClientsGrid.SelectedItem is not ClientRow row)
                return;
            selectedClient = row.Client;
            FormTitle.Text = "Карточка клиента";
            AddressBox.Text = row.Client.Address;

            if (row.Client is ClientLegalEntity legal)
            {
                ClientTypeBox.SelectedIndex = 1;
                CompanyBox.Text = legal.NameFirm;
            }
            else if (row.Client is ClientIndividual person)
            {
                ClientTypeBox.SelectedIndex = 0;
                LastNameBox.Text = person.LastName;
                FirstNameBox.Text = person.FirstName;
                MiddleNameBox.Text = person.MiddleName;
                PassportBox.Text = person.Passport;
            }

            ClientTypeBox.IsEnabled = false;
        }

        private Client ReadForm()
        {
            if (ClientTypeBox.SelectedIndex == 1)
                return new ClientLegalEntity(CompanyBox.Text, AddressBox.Text);
            
            return new ClientIndividual(FirstNameBox.Text, LastNameBox.Text, MiddleNameBox.Text, AddressBox.Text, PassportBox.Text);
        }

        private void SaveClientClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Конструктор проверяет поля до изменения данных
                Client canidate = ReadForm();
                Client savedClient;

                if (selectedClient is Client existing)
                {
                    Client previous = CopyClient(existing);
                    ApplyChanges(existing, canidate);

                    try
                    {
                        storage.Save(dataPath, data);
                    }
                    catch
                    {
                        ApplyChanges(existing, previous);
                        throw;
                    }
                    savedClient = existing;
                }
                else
                {
                    data.Clients.Add(canidate);

                    try
                    {
                        storage.Save(dataPath, data);
                    }
                    catch
                    {
                        data.Clients.Remove(canidate);
                        throw;
                    }
                    savedClient = canidate;
                }

                RefreshClients(savedClient);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Не удалось сохранить клиента", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static Client CopyClient(Client client)
        {
            if (client is ClientLegalEntity legal)
                return new ClientLegalEntity(legal.NameFirm, legal.Address);
            
            var person = (ClientIndividual)client;
            return new ClientIndividual(person.FirstName, person.LastName, person.MiddleName, person.Address, person.Passport);
        }

        private static void ApplyChanges(Client target, Client source)
        {
            if (target is ClientLegalEntity legal && source is ClientLegalEntity newLegal)
                legal.ChangeNameFirm(newLegal.NameFirm);
            else if (target is ClientIndividual person && source is ClientIndividual newPerson)
                person.UpdatePersonalData(newPerson.FirstName, newPerson.LastName, newPerson.MiddleName, newPerson.Passport);
            else
                throw new InvalidOperationException("Тип существующего клиента изменить нельзя.");

            target.ChangeAddress(source.Address);  
        }
    }
}