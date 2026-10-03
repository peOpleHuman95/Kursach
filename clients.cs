namespace Security_agent
{
    public abstract class Agreement
    {
        public string? NumberAgreement {get; set;}
        public DateTime? DateAgreement {get; protected set;}
        public DateTime? DateEndContract {get; protected set;}
        public DateTime DateEvent {get; set;}
        public decimal Sum {get; set;}
        public DateTime? DatePayment {get; set;}
        public string? NumberDocument {get; set;}

    }
    public abstract class ClientLegalEntity : Agreement
    {
        // Адрес клиента
        public string NameFirm {get; private set;}
        public string Address {get; private set;}
        
        
        protected ClientLegalEntity(
            string nameFirm,
            string address,
            string numberAgreement,
            DateTime dateAgreement,
            DateTime dateEndContract)
        {
            if (string.IsNullOrWhiteSpace(nameFirm)) 
                throw new ArgumentException("Название фирмы не может быть пустым.", nameof(nameFirm));
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("Адрес не может быть пустым.", nameof(address));
            if (string.IsNullOrWhiteSpace(numberAgreement))
                throw new ArgumentException("Номер договора не может быть пустым.", nameof(numberAgreement));
            if (dateEndContract < dateAgreement)
                throw new ArgumentException("Дата окончания не может быть раньше даты заключения.", nameof(dateEndContract));
            
            NameFirm = nameFirm;
            Address = address;
            NumberAgreement = numberAgreement;
            DateAgreement = dateAgreement;
            DateEndContract = dateEndContract;

        }
        public void ChangeAddress(string newAddress)
        {
            if (string.IsNullOrWhiteSpace(newAddress)) throw new ArgumentException("Адрес не может быть пустым", nameof(newAddress));
            if (newAddress == Address) throw new InvalidOperationException("Адрес клиента остался тот же.");
            Address = newAddress;
        }
        public void ChangeNameFirm(string newNameFirm)
        {
            if (string.IsNullOrWhiteSpace(newNameFirm)) throw new ArgumentException("Название не может быть пустым", nameof(newNameFirm));
            if (newNameFirm == NameFirm) throw new InvalidOperationException("Название фирмы осталось тем же.");
            NameFirm = newNameFirm;
        }
    }
    public abstract class ClientIndividual :Agreement
    {
        public string FirstName {get; set;} = "";
        public string LastName {get; set;} = "";
        public string MiddleName {get; set;} = "";
        public string ClientAddress {get; set;} = "";
        public string ClientPassport {get; set;} = "";

    }
}
