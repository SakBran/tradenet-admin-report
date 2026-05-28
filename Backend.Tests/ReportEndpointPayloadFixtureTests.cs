namespace Backend.Tests;

public sealed class ReportEndpointPayloadFixtureTests
{
    [Fact]
    public void Request_payload_fixture_records_every_report_endpoint()
    {
        var payloads = ReportTestHelper.ControllerTypes
            .ToDictionary(
                controllerType => controllerType.Name,
                controllerType => ReportTestHelper.ToPayloadDictionary(
                    ReportTestHelper.CreateRequest(controllerType)));

        Assert.Equal(ReportTestHelper.ControllerTypes.Count, payloads.Count);

        foreach (var controllerType in ReportTestHelper.ControllerTypes)
        {
            var payload = payloads[controllerType.Name];

            Assert.True(payload.Count > 0, $"{controllerType.Name} should have a non-empty request payload.");
            Assert.Equal(10, payload[nameof(API.Model.ReportQueryRequest.PageSize)]);
            Assert.Equal(0, payload[nameof(API.Model.ReportQueryRequest.PageIndex)]);

            if (payload.ContainsKey("FromDate"))
            {
                Assert.Equal(ReportTestHelper.FromDate, payload["FromDate"]);
            }

            if (payload.ContainsKey("ToDate"))
            {
                Assert.Equal(ReportTestHelper.ToDate, payload["ToDate"]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public void Request_payload_fixture_is_accepted_by_controller_request_factory(Type controllerType)
    {
        var procedureRequest = ReportTestHelper.CreateProcedureRequest(controllerType);

        Assert.NotNull(procedureRequest);
    }

    public static IEnumerable<object[]> Controllers() => ReportTestHelper.ControllerCases();
}
