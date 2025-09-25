using System;

namespace TravelAgency.Web.Models
{
    public class CartItem
    {
        public Guid PackageId { get; set; }
        public string Title { get; set; } = "";
        public int PeopleCount { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal => UnitPrice * PeopleCount;
    }
}
