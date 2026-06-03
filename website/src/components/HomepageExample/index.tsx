import React from 'react';
import clsx from 'clsx';
import CodeBlock from '@theme/CodeBlock';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

const codeExample = `using var mock = new HttpMock();

// 1. Match with full URL shortcuts and wildcards
// 2. Filter by specific query parameters
// 3. Use custom JSON options for serialization
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

mock.ForGet("https://api.github.com/repos/*/issues?state=open")
    .WithQueryParam("page", "1")
    .Using(options)
    .RespondsWithJsonContent(new[] { 
        new { Id = 1, Title = "Found a bug" },
        new { Id = 2, Title = "Feature request" }
    });

// 4. Match Bearer tokens and other headers
// 5. Match request body using JSON equivalence
// 6. Capture requests for later verification
var creations = new RequestCollection();

mock.ForPost()
    .WithPath("/api/users")
    .WithBearerToken("secret-token")
    .WithBody(new { Name = "John", Role = "Admin" })
    .CollectingRequestsIn(creations)
    .RespondsWithStatus(HttpStatusCode.Created);

// 7. Built-in support for Problem Details (RFC 7807)
mock.ForGet("/api/users/999")
    .RespondsWithProblemDetails(HttpStatusCode.NotFound, "User not found");

// Get the pre-configured HttpClient and start testing!
var client = mock.GetClient();

// 8. Assert your expectations with FluentAssertions
mock.Should().HaveAllRequestsCalled();
creations.Should().ContainRequestFor("/api/users")
    .Which.HasHeader("X-Trace-Id");`;

export default function HomepageExample(): JSX.Element {
  return (
    <section className={styles.exampleSection}>
      <div className="container">
        <div className="row">
          <div className="col col--12">
            <Heading as="h2" className="text--center margin-bottom--lg">
              Power in Simplicity
            </Heading>
            <div className={styles.codeWrapper}>
              <CodeBlock language="csharp">
                {codeExample}
              </CodeBlock>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
