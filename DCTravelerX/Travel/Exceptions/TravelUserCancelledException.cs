using System;

namespace DCTravelerX.Travel.Exceptions;

internal sealed class TravelUserCancelledException
(
    string message
) : Exception(message);
