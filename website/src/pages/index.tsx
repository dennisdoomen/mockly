import type {ReactNode} from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import HomepageFeatures from '@site/src/components/HomepageFeatures';
import HomepageExample from '@site/src/components/HomepageExample';
import Heading from '@theme/Heading';

import styles from './index.module.css';

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero', styles.heroBanner)}>
      <div className="container">
        <div className={styles.heroEyebrow}>Open Source</div>
        <Heading as="h1" className={clsx('hero__title', styles.heroTitle)}>
          {siteConfig.title}
        </Heading>
        <p className={styles.heroSubtitle}>{siteConfig.tagline}</p>
        <div className={styles.buttons}>
          <Link
            className="button button--secondary button--lg"
            to="/docs/">
            Get Started - 5min ⏱️
          </Link>
        </div>
        <div className={styles.heroPanel}>
          <code className={styles.heroCode}>
            mock<span className={styles.heroCodeDot}>.</span>ForGet
            <span className={styles.heroCodePunct}>()</span>
            <span className={styles.heroCodeDot}>.</span>WithPath
            <span className={styles.heroCodePunct}>(</span>
            <span className={styles.heroCodeString}>"/api/users/*"</span>
            <span className={styles.heroCodePunct}>)</span>
          </code>
          <div className={styles.heroCaption}>
            // fluent chaining, wildcards included — no separate matcher classes
          </div>
        </div>
      </div>
    </header>
  );
}

export default function Home(): ReactNode {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout
      title={`${siteConfig.title} - Fluent HTTP mocking for .NET`}
      description="Fluent HTTP mocking for .NET like it should have been done">
      <HomepageHeader />
      <main>
        <HomepageExample />
        <HomepageFeatures />
      </main>
    </Layout>
  );
}
