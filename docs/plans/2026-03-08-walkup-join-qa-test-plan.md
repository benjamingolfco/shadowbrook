# Walk-Up Join QA Test Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fill integration test gaps for the walk-up waitlist join flow (issue #31)

**Architecture:** Additional integration tests in WalkUpJoinEndpointsTests.cs covering edge cases, cross-feature integration, phone normalization variants, name trimming, existing golfer preservation, and the full verify→join→confirm flow

**Tech Stack:** xUnit, TestWebApplicationFactory (SQLite in-memory), HttpClient

---

### Task 1: Verify phone format normalization during join

Golfers may enter phone numbers in various formats. The join endpoint normalizes via PhoneNormalizer but tests only cover one format. Verify multiple formats all resolve to the same golfer.

**Files:**
- Modify: `src/backend/Shadowbrook.Api.Tests/WalkUpJoinEndpointsTests.cs`

**Tests to add:**

```csharp
[Fact]
public async Task Join_DifferentPhoneFormats_SameWaitlist_Returns409()
{
    // Two joins with different formats of same number = duplicate
    var (_, _, shortCode) = await CreateOpenWaitlistAsync();
    var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

    await PostJoinAsync(verifyBody!.CourseWaitlistId, "Pat", "Test", "555-444-3333");
    var response = await PostJoinAsync(verifyBody.CourseWaitlistId, "Pat", "Test", "(555) 444-3333");

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
}

[Fact]
public async Task Join_E164PhoneFormat_MatchesExisting()
{
    var (_, _, shortCode) = await CreateOpenWaitlistAsync();
    var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

    await PostJoinAsync(verifyBody!.CourseWaitlistId, "Pat", "Test", "555-444-5555");
    var response = await PostJoinAsync(verifyBody.CourseWaitlistId, "Pat", "Test", "+15554445555");

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
}
```

---

### Task 2: Verify existing golfer name is NOT overwritten

After the code review fix, existing golfer names should not be modified. The join response should still show the name from the current request (denormalized on the entry), but the underlying Golfer record stays unchanged.

**Tests to add:**

```csharp
[Fact]
public async Task Join_ExistingGolfer_EntryUsesNewName_ButGolferPreserved()
{
    // First join creates golfer as "Original Name"
    var (_, _, shortCode1) = await CreateOpenWaitlistAsync();
    var v1 = await (await PostVerifyAsync(shortCode1)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
    var phone = $"555-{Random.Shared.Next(100, 999)}-{Random.Shared.Next(1000, 9999)}";
    var r1 = await PostJoinAsync(v1!.CourseWaitlistId, "Original", "Name", phone);
    Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
    var b1 = await r1.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
    Assert.Equal("Original Name", b1!.GolferName);

    // Second join on different waitlist with different name
    var (_, _, shortCode2) = await CreateOpenWaitlistAsync();
    var v2 = await (await PostVerifyAsync(shortCode2)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
    var r2 = await PostJoinAsync(v2!.CourseWaitlistId, "Changed", "Name", phone);
    Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

    // Entry uses new name (denormalized on the entry)
    var b2 = await r2.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
    Assert.Equal("Changed Name", b2!.GolferName);
}
```

---

### Task 3: Verify name trimming

Names with leading/trailing whitespace should be trimmed in the response.

**Tests to add:**

```csharp
[Fact]
public async Task Join_WhitespaceInName_IsTrimmed()
{
    var (_, _, shortCode) = await CreateOpenWaitlistAsync();
    var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();

    var response = await PostJoinAsync(verifyBody!.CourseWaitlistId, "  John  ", "  Smith  ", "555-888-7777");

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
    Assert.Equal("John Smith", body!.GolferName);
}
```

---

### Task 4: Verify FluentValidation error format

After switching to FluentValidation, error responses should still return proper messages. Verify the validation filter returns the expected format.

**Tests to add:**

```csharp
[Fact]
public async Task Verify_NullCode_Returns400_WithMessage()
{
    var response = await this.client.PostAsJsonAsync("/walkup/verify", new { Code = (string?)null });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ValidationErrorResponse>();
    Assert.NotNull(body);
    Assert.NotEmpty(body!.Error);
}

[Fact]
public async Task Join_AllFieldsMissing_Returns400()
{
    var response = await PostJoinAsync(Guid.NewGuid(), "", "", "");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}
```

Add the DTO:
```csharp
private record ValidationErrorResponse(string Error);
```

---

### Task 5: Full end-to-end flow test

Test the complete happy path: create tenant → create course → open waitlist → verify code → join waitlist → verify position and course name.

**Tests to add:**

```csharp
[Fact]
public async Task FullFlow_CreateTenant_OpenWaitlist_VerifyCode_JoinWaitlist()
{
    // Setup
    var tenantId = await CreateTestTenantAsync();
    var courseName = $"Full Flow Course {Guid.NewGuid()}";

    var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
    createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
    createRequest.Content = JsonContent.Create(new { Name = courseName });
    var createResponse = await this.client.SendAsync(createRequest);
    var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

    // Open waitlist
    var openResponse = await PostOpenWaitlistAsync(course!.Id);
    Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);
    var waitlist = await openResponse.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();

    // Verify code
    var verifyResponse = await PostVerifyAsync(waitlist!.ShortCode);
    Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
    var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<VerifyCodeResponse>();
    Assert.Equal(courseName, verifyBody!.CourseName);

    // Join waitlist
    var joinResponse = await PostJoinAsync(verifyBody.CourseWaitlistId, "E2E", "Golfer", "555-999-1111");
    Assert.Equal(HttpStatusCode.Created, joinResponse.StatusCode);
    var joinBody = await joinResponse.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
    Assert.Equal("E2E Golfer", joinBody!.GolferName);
    Assert.Equal(1, joinBody.Position);
    Assert.Equal(courseName, joinBody.CourseName);
}
```

---

### Task 6: Cross-waitlist duplicate (same golfer, different waitlists = OK)

Verify the same phone can join multiple waitlists without conflict — only same-phone + same-waitlist is a duplicate.

**Tests to add:**

```csharp
[Fact]
public async Task Join_SamePhone_DifferentWaitlists_BothSucceed_PositionIsOne()
{
    var phone = $"555-{Random.Shared.Next(100, 999)}-{Random.Shared.Next(1000, 9999)}";

    var (_, _, sc1) = await CreateOpenWaitlistAsync();
    var v1 = await (await PostVerifyAsync(sc1)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
    var r1 = await PostJoinAsync(v1!.CourseWaitlistId, "Cross", "Test", phone);
    Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
    var b1 = await r1.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
    Assert.Equal(1, b1!.Position);

    var (_, _, sc2) = await CreateOpenWaitlistAsync();
    var v2 = await (await PostVerifyAsync(sc2)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
    var r2 = await PostJoinAsync(v2!.CourseWaitlistId, "Cross", "Test", phone);
    Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
    var b2 = await r2.Content.ReadFromJsonAsync<JoinWaitlistResponse>();
    Assert.Equal(1, b2!.Position); // First on THIS waitlist
}
```

---

### Task 7: Verify course name matches in verify response

Ensure the verify endpoint returns the correct course name (not a different course's name).

**Tests to add:**

```csharp
[Fact]
public async Task Verify_ReturnsCorrectCourseName()
{
    var tenantId = await CreateTestTenantAsync();
    var expectedName = $"Specific Course {Guid.NewGuid()}";

    var createRequest = new HttpRequestMessage(HttpMethod.Post, "/courses");
    createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
    createRequest.Content = JsonContent.Create(new { Name = expectedName });
    var createResponse = await this.client.SendAsync(createRequest);
    var course = await createResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

    var openResponse = await PostOpenWaitlistAsync(course!.Id);
    var waitlist = await openResponse.Content.ReadFromJsonAsync<WalkUpWaitlistResponse>();

    var verifyResponse = await PostVerifyAsync(waitlist!.ShortCode);
    var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<VerifyCodeResponse>();

    Assert.Equal(expectedName, verifyBody!.CourseName);
}
```

---

### Task 8: Duplicate position reflects actual queue position

When a duplicate join returns 409, the position should reflect the golfer's actual position in the queue (not always 1).

**Tests to add:**

```csharp
[Fact]
public async Task Join_DuplicateAfterOthers_Returns409_WithCorrectPosition()
{
    var (_, _, shortCode) = await CreateOpenWaitlistAsync();
    var verifyBody = await (await PostVerifyAsync(shortCode)).Content.ReadFromJsonAsync<VerifyCodeResponse>();
    var waitlistId = verifyBody!.CourseWaitlistId;

    // First two golfers join
    await PostJoinAsync(waitlistId, "First", "Person", "555-100-0001");
    await PostJoinAsync(waitlistId, "Second", "Person", "555-100-0002");

    // Third golfer joins
    var thirdPhone = "555-100-0003";
    await PostJoinAsync(waitlistId, "Third", "Person", thirdPhone);

    // Third golfer tries again — should get position 3
    var dup = await PostJoinAsync(waitlistId, "Third", "Person", thirdPhone);
    Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

    var body = await dup.Content.ReadFromJsonAsync<DuplicateEntryError>();
    Assert.Equal(3, body!.Position);
}
```
