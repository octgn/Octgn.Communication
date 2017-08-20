namespace Octgn.Communication.Messages
{
    public enum LoginResultType
    {
        UnknownError,
        Ok,
        EmailUnverified,
        UnknownUsername,
        PasswordWrong,
        NotSubscribed,
        NoEmailAssociated
    }
}
