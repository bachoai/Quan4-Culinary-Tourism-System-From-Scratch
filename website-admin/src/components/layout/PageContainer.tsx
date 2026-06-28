import { Space, Typography } from 'antd';
import type { PropsWithChildren, ReactNode } from 'react';
import { AnimatedPage } from '../common/AnimatedPage';

interface PageContainerProps extends PropsWithChildren {
  title: string;
  subtitle?: string;
  extra?: ReactNode;
}

export function PageContainer({ title, subtitle, extra, children }: PageContainerProps) {
  return (
    <AnimatedPage>
      <Space direction="vertical" size="large" style={{ width: '100%' }}>
        <div className="page-heading">
          <div className="page-heading-content">
            <Typography.Title level={2} style={{ marginBottom: 4 }}>
              {title}
            </Typography.Title>
            {subtitle ? <Typography.Text type="secondary">{subtitle}</Typography.Text> : null}
          </div>
          {extra ? <div className="page-heading-extra">{extra}</div> : null}
        </div>
        {children}
      </Space>
    </AnimatedPage>
  );
}
