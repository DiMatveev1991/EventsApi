namespace EventsApi.DTOs
{
    /// <summary>
    /// Статус брони.
    /// </summary>
    public enum BookingStatus
    {
        /// <summary>Бронь создана, ожидает обработки фоновым сервисом.</summary>
        Pending = 0,

        /// <summary>Бронь подтверждена.</summary>
        Confirmed = 1,

        /// <summary>Бронь отклонена.</summary>
        Rejected = 2
    }
}
