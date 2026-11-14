using System;
using System.Windows;
using System.Windows.Controls;

namespace Security_agent
{
    // Открывает форму создания или редактирования карточки клиента.
    public partial class ClientEditorWindow : Window
    {
        private readonly AgencyData data;
        private readonly Storage storage;
        private readonly string dataPath;
        private readonly Client? selectedClient;

        public Client? SavedClient { get; private set; }

        // Подключает разметку окна и подготавливает его к работе.
        public ClientEditorWindow(AgencyData data, Storage storage, string dataPath, Client? client)
        {
            this.data = data;
            this.storage = storage;
            this.dataPath = dataPath;
            selectedClient = client;

            InitializeComponent();
            ClientTypeBox.SelectedIndex = client is ClientLegalEntity ? 1 : 0;
            if (client == null)
                return;

            Title = "Карточка клиента";
            ClientTypeBox.IsEnabled = false;
            AddressBox.Text = client.Address;

            if (client is ClientLegalEntity legal)
            {
                CompanyBox.Text = legal.NameFirm;
            }
            else if (client is ClientIndividual person)
            {
                LastNameBox.Text = person.LastName;
                FirstNameBox.Text = person.FirstName;
                MiddleNameBox.Text = person.MiddleName;
                PassportBox.Text = person.Passport;
            }
        }

        // Показывает поля, соответствующие выбранному типу клиента.
        private void ClientTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            bool isLegal = ClientTypeBox.SelectedIndex == 1;
            LegalFields.Visibility = isLegal ? Visibility.Visible : Visibility.Collapsed;
            IndividualFields.Visibility = isLegal ? Visibility.Collapsed : Visibility.Visible;
        }

        // Создаёт клиента из введённых реквизитов.
        private Client ReadForm()
        {
            if (ClientTypeBox.SelectedIndex == 1)
                return new ClientLegalEntity(CompanyBox.Text, AddressBox.Text);
            
            return new ClientIndividual(FirstNameBox.Text, LastNameBox.Text, MiddleNameBox.Text, AddressBox.Text, PassportBox.Text);
        }

        // Сохраняет карточку и отменяет изменение данных при ошибке записи.
        private void SaveClientClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Конструктор проверяет поля до изменения данных
                Client candidate = ReadForm();
                Client savedClient;

                if (selectedClient is Client existing)
                {
                    Client previous = CopyClient(existing);
                    ApplyChanges(existing, candidate);

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
                    data.Clients.Add(candidate);

                    try
                    {
                        storage.Save(dataPath, data);
                    }
                    catch
                    {
                        data.Clients.Remove(candidate);
                        throw;
                    }
                    savedClient = candidate;
                }

                SavedClient = savedClient;
                DialogResult = true;
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Не удалось сохранить клиента", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Создаёт копию карточки для восстановления при ошибке сохранения.
        private static Client CopyClient(Client client)
        {
            if (client is ClientLegalEntity legal)
                return new ClientLegalEntity(legal.NameFirm, legal.Address);
            
            var person = (ClientIndividual)client;
            return new ClientIndividual(person.FirstName, person.LastName, person.MiddleName, person.Address, person.Passport);
        }

        // Переносит реквизиты в существующую карточку того же типа.
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