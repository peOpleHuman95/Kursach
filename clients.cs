namespace Security_agent
{
    public abstract class Client
    {
        // Адрес клиента
        public string Address {get; private set;}
        protected Client(string address)
        {
            Address = address;
        }
        public void ChangeAddress(string newAddress)
        {
            if (newAddress == null) throw new ArgumentNullException(nameof(newAddress));
            if (newAddress == Address) throw new InvalidOperationException("Адрес клиента остался тот же.");
            Address = newAddress;
        }
    }
}