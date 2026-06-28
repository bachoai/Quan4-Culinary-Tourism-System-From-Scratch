import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Input, Select, Space, Table, Tag } from 'antd';
import { useMemo, useState } from 'react';
import { adminApi } from '../api/adminApi';
import { useI18n } from '../i18n/provider';
import { USER_ROLES } from '../utils/constants';
import { formatDateTime } from '../utils/format';
import { PageContainer } from '../components/layout/PageContainer';

export function UserManagementPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [role, setRole] = useState<string | undefined>();
  const usersQuery = useQuery({ queryKey: ['users'], queryFn: adminApi.getUsers });

  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => adminApi.updateUserStatus(id, { isActive }),
    onSuccess: () => {
      notification.success({ message: t('user_status_updated') });
      queryClient.invalidateQueries({ queryKey: ['users'] });
    },
  });

  const roleMutation = useMutation({
    mutationFn: ({ id, roles }: { id: string; roles: string[] }) => adminApi.updateUserRoles(id, { roles }),
    onSuccess: () => {
      notification.success({ message: t('user_roles_updated') });
      queryClient.invalidateQueries({ queryKey: ['users'] });
    },
  });

  const data = useMemo(() => {
    return (usersQuery.data ?? []).filter((item) => {
      const matchesKeyword = !keyword || `${item.fullName} ${item.email}`.toLowerCase().includes(keyword.toLowerCase());
      const matchesRole = !role || item.roles.includes(role);
      return matchesKeyword && matchesRole;
    });
  }, [keyword, role, usersQuery.data]);

  return (
    <PageContainer title={t('users_title')} subtitle={t('users_subtitle')}>
      <Card className="glass-card">
        <Space wrap className="page-toolbar" style={{ marginBottom: 16 }}>
          <Input.Search placeholder={t('users_search')} allowClear onChange={(event) => setKeyword(event.target.value)} />
          <Select allowClear placeholder={t('role')} style={{ width: 160 }} options={USER_ROLES.map((value) => ({ value, label: value }))} onChange={setRole} />
        </Space>
        <Table
          className="table-responsive"
          rowKey="id"
          dataSource={data}
          loading={usersQuery.isFetching}
          scroll={{ x: 1100 }}
          columns={[
            { title: t('full_name'), dataIndex: 'fullName' },
            { title: t('email'), dataIndex: 'email' },
            { title: t('owner_status'), dataIndex: 'ownerStatus' },
            { title: t('created_at'), render: (_, record) => formatDateTime(record.createdAt) },
            { title: t('last_login'), render: (_, record) => formatDateTime(record.lastLoginAt) },
            {
              title: t('roles'),
              render: (_, record) => (
                <Select
                  mode="multiple"
                  value={record.roles}
                  style={{ minWidth: 220 }}
                  options={USER_ROLES.map((value) => ({ value, label: value }))}
                  onChange={(roles) => roleMutation.mutate({ id: record.id, roles })}
                />
              ),
            },
            {
              title: t('status'),
              render: (_, record) => (
                <Tag color={record.isActive ? 'green' : 'red'}>{record.isActive ? t('active') : t('inactive')}</Tag>
              ),
            },
            {
              title: t('actions'),
              render: (_, record) => (
                <Button onClick={() => statusMutation.mutate({ id: record.id, isActive: !record.isActive })}>
                  {record.isActive ? t('disable') : t('enable')}
                </Button>
              ),
            },
          ]}
        />
      </Card>
    </PageContainer>
  );
}
