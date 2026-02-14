import type {ReactNode} from 'react';
import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

type FeatureItem = {
  title: string;
  emoji: string;
  description: ReactNode;
};

const FeatureList: FeatureItem[] = [
  {
    title: 'Fluent API',
    emoji: '🎯',
    description: (
      <>
        Chain method calls to build complex HTTP mocking scenarios with ease.
        Intuitive and readable API that makes your test code self-documenting.
      </>
    ),
  },
  {
    title: 'Wildcard Matching',
    emoji: '✨',
    description: (
      <>
        Use wildcards in URLs to match patterns. Perfect for dynamic paths,
        query strings, and flexible request matching.
      </>
    ),
  },
  {
    title: 'Request Capture',
    emoji: '🔍',
    description: (
      <>
        Automatically capture all requests with full metadata including headers,
        body, and timestamp for detailed test assertions.
      </>
    ),
  },
  {
    title: 'Powerful Assertions',
    emoji: '✅',
    description: (
      <>
        Built-in FluentAssertions extensions for verifying HTTP behavior with
        expressive and readable test assertions.
      </>
    ),
  },
  {
    title: 'Zero Configuration',
    emoji: '⚡',
    description: (
      <>
        Works out of the box with sensible defaults. Start mocking HTTP requests
        in seconds without complex setup.
      </>
    ),
  },
  {
    title: 'Fail-Fast Testing',
    emoji: '🛡️',
    description: (
      <>
        Throws clear exceptions for unexpected requests by default, helping you
        catch configuration issues early in testing.
      </>
    ),
  },
];

function Feature({title, emoji, description}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center">
        <div className={styles.featureEmoji}>{emoji}</div>
      </div>
      <div className="text--center padding-horiz--md">
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): ReactNode {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
