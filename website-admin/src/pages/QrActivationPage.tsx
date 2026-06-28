import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Input, Modal, QRCode, Select, Space, Statistic, Switch, Table, Tag, Typography } from 'antd';
import { Download, Plus, Printer } from 'lucide-react';
import { useEffect, useMemo, useRef, useState, type Key } from 'react';
import { qrActivationApi } from '../api/qrActivationApi';
import { ConfirmDeleteButton } from '../components/common/ConfirmDeleteButton';
import { LoadingScreen } from '../components/common/LoadingScreen';
import { QrActivationForm } from '../components/forms/QrActivationForm';
import { PageContainer } from '../components/layout/PageContainer';
import { useI18n } from '../i18n/provider';
import type { QrActivationResponse } from '../types/responses';
import { formatDateTime } from '../utils/format';

const ALL_ZONES = '__all_zones__';

function compareQrActivations(left: QrActivationResponse, right: QrActivationResponse) {
  return left.stopZone.localeCompare(right.stopZone)
    || left.sortOrder - right.sortOrder
    || left.title.localeCompare(right.title)
    || left.code.localeCompare(right.code);
}

function escapeCsv(value: unknown) {
  const text = String(value ?? '');
  return `"${text.replaceAll('"', '""')}"`;
}

function buildCsv(rows: QrActivationResponse[]) {
  const header = [
    'code',
    'title',
    'stopZone',
    'stopAddress',
    'poiName',
    'poiAddress',
    'poiWard',
    'scanMode',
    'deepLink',
    'sortOrder',
    'isActive',
  ];

  const body = rows.map((row) => ([
    row.code,
    row.title,
    row.stopZone,
    row.stopAddress ?? '',
    row.poiName,
    row.poiAddress,
    row.poiWard,
    row.scanMode,
    row.deepLink,
    row.sortOrder,
    row.isActive ? 'true' : 'false',
  ].map(escapeCsv).join(',')));

  return [header.join(','), ...body].join('\n');
}

function buildPrintDocument(title: string, contentHtml: string) {
  return `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <title>${title}</title>
  <style>
    body {
      margin: 0;
      padding: 24px;
      font-family: Arial, sans-serif;
      background: #f8fafc;
      color: #0f172a;
    }
    .qr-print-sheet {
      display: flex;
      flex-direction: column;
      gap: 24px;
    }
    .qr-zone-block {
      page-break-inside: avoid;
      break-inside: avoid;
    }
    .qr-zone-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 16px;
    }
    .qr-print-card {
      border: 1px solid #cbd5e1;
      border-radius: 18px;
      padding: 16px;
      background: #ffffff;
      page-break-inside: avoid;
      break-inside: avoid;
    }
    .qr-print-card svg {
      width: 112px;
      height: 112px;
    }
    @media print {
      body {
        padding: 0;
        background: #ffffff;
      }
      .qr-zone-grid {
        gap: 12px;
      }
      .qr-print-card {
        border-color: #94a3b8;
      }
    }
  </style>
</head>
<body>${contentHtml}</body>
</html>`;
}

function downloadTextFile(fileName: string, content: string, contentType: string) {
  const blob = new Blob([content], { type: contentType });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

function toFileSafeName(value: string) {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '') || 'qr-code';
}

function loadImage(url: string) {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('Failed to load QR image.'));
    image.src = url;
  });
}

async function downloadSvgAsPng(svgElement: SVGSVGElement, fileName: string) {
  const serializer = new XMLSerializer();
  let source = serializer.serializeToString(svgElement);

  if (!source.includes('xmlns="http://www.w3.org/2000/svg"')) {
    source = source.replace('<svg', '<svg xmlns="http://www.w3.org/2000/svg"');
  }

  const svgBlob = new Blob([source], { type: 'image/svg+xml;charset=utf-8' });
  const svgUrl = URL.createObjectURL(svgBlob);

  try {
    const image = await loadImage(svgUrl);
    const viewBox = svgElement.viewBox.baseVal;
    const width = viewBox?.width || Number(svgElement.getAttribute('width')) || image.width || 256;
    const height = viewBox?.height || Number(svgElement.getAttribute('height')) || image.height || 256;
    const canvas = document.createElement('canvas');
    const scale = 4;
    canvas.width = width * scale;
    canvas.height = height * scale;

    const context = canvas.getContext('2d');
    if (!context) {
      throw new Error('Canvas context is unavailable.');
    }

    context.fillStyle = '#ffffff';
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(image, 0, 0, canvas.width, canvas.height);

    const anchor = document.createElement('a');
    anchor.href = canvas.toDataURL('image/png');
    anchor.download = fileName;
    anchor.click();
  } finally {
    URL.revokeObjectURL(svgUrl);
  }
}

export function QrActivationPage() {
  const { t } = useI18n();
  const { notification } = App.useApp();
  const queryClient = useQueryClient();
  const previewRef = useRef<HTMLDivElement | null>(null);
  const qrImageRef = useRef<HTMLDivElement | null>(null);
  const [keyword, setKeyword] = useState('');
  const [zoneFilter, setZoneFilter] = useState(ALL_ZONES);
  const [activeOnly, setActiveOnly] = useState(false);
  const [selectedRowKeys, setSelectedRowKeys] = useState<Key[]>([]);
  const [editing, setEditing] = useState<QrActivationResponse | null>(null);
  const [previewingQr, setPreviewingQr] = useState<QrActivationResponse | null>(null);
  const [open, setOpen] = useState(false);

  const query = useQuery({ queryKey: ['qr-activations'], queryFn: qrActivationApi.getAll });

  const createMutation = useMutation({
    mutationFn: qrActivationApi.create,
    onSuccess: () => {
      notification.success({ message: t('qr_activation_created') });
      setOpen(false);
      queryClient.invalidateQueries({ queryKey: ['qr-activations'] });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: Parameters<typeof qrActivationApi.update>[1] }) => qrActivationApi.update(id, payload),
    onSuccess: () => {
      notification.success({ message: t('qr_activation_updated') });
      setOpen(false);
      setEditing(null);
      queryClient.invalidateQueries({ queryKey: ['qr-activations'] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: qrActivationApi.delete,
    onSuccess: () => {
      notification.success({ message: t('qr_activation_deleted') });
      queryClient.invalidateQueries({ queryKey: ['qr-activations'] });
    },
  });

  const items = useMemo(() => [...(query.data ?? [])].sort(compareQrActivations), [query.data]);

  const zoneOptions = useMemo(
    () => [
      { value: ALL_ZONES, label: t('qr_zone_all') },
      ...Array.from(new Set(items.map((item) => item.stopZone).filter(Boolean)))
        .sort((left, right) => left.localeCompare(right))
        .map((zone) => ({ value: zone, label: zone })),
    ],
    [items, t],
  );

  const filteredItems = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLowerCase();
    return items.filter((item) => {
      if (activeOnly && !item.isActive) {
        return false;
      }

      if (zoneFilter !== ALL_ZONES && item.stopZone !== zoneFilter) {
        return false;
      }

      if (!normalizedKeyword) {
        return true;
      }

      return `${item.code} ${item.title} ${item.stopZone} ${item.stopAddress ?? ''} ${item.poiName} ${item.poiAddress}`.toLowerCase().includes(normalizedKeyword);
    });
  }, [activeOnly, items, keyword, zoneFilter]);

  const selectedItems = useMemo(() => {
    const selectedIdSet = new Set(selectedRowKeys.map(String));
    return items.filter((item) => selectedIdSet.has(item.id)).sort(compareQrActivations);
  }, [items, selectedRowKeys]);

  const groupedSelectedItems = useMemo(() => {
    const map = new Map<string, QrActivationResponse[]>();
    for (const item of selectedItems) {
      const zone = item.stopZone || t('qr_zone_all');
      const group = map.get(zone);
      if (group) {
        group.push(item);
      } else {
        map.set(zone, [item]);
      }
    }

    return Array.from(map.entries())
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([zone, records]) => ({ zone, records: records.sort(compareQrActivations) }));
  }, [selectedItems, t]);

  useEffect(() => {
    const existingIds = new Set(items.map((item) => item.id));
    setSelectedRowKeys((current) => current.filter((key) => existingIds.has(String(key))));
  }, [items]);

  const ensureSelection = () => {
    if (selectedItems.length > 0) {
      return true;
    }

    notification.warning({ message: t('qr_select_at_least_one') });
    return false;
  };

  const handlePrint = () => {
    if (!ensureSelection() || !previewRef.current) {
      return;
    }

    const printWindow = window.open('', '_blank', 'noopener,noreferrer');
    if (!printWindow) {
      return;
    }

    printWindow.document.write(buildPrintDocument(t('qr_preview_title'), previewRef.current.innerHTML));
    printWindow.document.close();
    printWindow.focus();
    printWindow.print();
  };

  const handleExportHtml = () => {
    if (!ensureSelection() || !previewRef.current) {
      return;
    }

    downloadTextFile('qr-bus-stop-sheet.html', buildPrintDocument(t('qr_preview_title'), previewRef.current.innerHTML), 'text/html;charset=utf-8');
    notification.success({ message: t('qr_exported_html') });
  };

  const handleExportCsv = () => {
    if (!ensureSelection()) {
      return;
    }

    downloadTextFile('qr-bus-stop-sheet.csv', buildCsv(selectedItems), 'text/csv;charset=utf-8');
    notification.success({ message: t('qr_exported_csv') });
  };

  const handleDownloadQrImage = async () => {
    if (!previewingQr || !qrImageRef.current) {
      notification.warning({ message: t('qr_image_unavailable') });
      return;
    }

    const svgElement = qrImageRef.current.querySelector('svg');
    if (!svgElement) {
      notification.warning({ message: t('qr_image_unavailable') });
      return;
    }

    try {
      await downloadSvgAsPng(svgElement, `${toFileSafeName(`${previewingQr.code}-${previewingQr.title}`)}.png`);
      notification.success({ message: t('qr_image_downloaded') });
    } catch {
      notification.error({ message: t('qr_image_download_failed') });
    }
  };

  if (query.isLoading) {
    return <LoadingScreen />;
  }

  return (
    <PageContainer
      title={t('qr_activations_title')}
      subtitle={t('qr_activations_subtitle')}
      extra={(
        <Button type="primary" icon={<Plus size={16} />} onClick={() => { setEditing(null); setOpen(true); }}>
          {t('qr_activations_new')}
        </Button>
      )}
    >
      <Card className="glass-card">
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
          <div className="page-heading">
            <div>
              <Typography.Title level={4} style={{ marginBottom: 4 }}>
                {t('qr_bulk_title')}
              </Typography.Title>
              <Typography.Text type="secondary">{t('qr_bulk_desc')}</Typography.Text>
            </div>
            <Space wrap>
              <Input.Search placeholder={t('qr_activations_search')} allowClear value={keyword} onChange={(event) => setKeyword(event.target.value)} style={{ width: 260 }} />
              <Select value={zoneFilter} onChange={setZoneFilter} options={zoneOptions} style={{ width: 220 }} placeholder={t('qr_zone_filter')} />
              <Space>
                <Typography.Text>{t('qr_active_only')}</Typography.Text>
                <Switch checked={activeOnly} onChange={setActiveOnly} />
              </Space>
            </Space>
          </div>
          <Space size="large" wrap>
            <Statistic title={t('qr_selected_count')} value={selectedItems.length} />
            <Statistic title={t('qr_zone_filter')} value={zoneFilter === ALL_ZONES ? t('qr_zone_all') : zoneFilter} />
            <Statistic title={t('status')} value={activeOnly ? t('active') : t('qr_zone_all')} />
          </Space>
          <Space wrap>
            <Button onClick={() => setSelectedRowKeys(filteredItems.map((item) => item.id))}>{t('qr_select_filtered')}</Button>
            <Button onClick={() => setSelectedRowKeys([])}>{t('qr_clear_selected')}</Button>
            <Button type="primary" icon={<Printer size={16} />} onClick={handlePrint}>{t('qr_print_selected')}</Button>
            <Button icon={<Download size={16} />} onClick={handleExportHtml}>{t('qr_export_html')}</Button>
            <Button icon={<Download size={16} />} onClick={handleExportCsv}>{t('qr_export_csv')}</Button>
          </Space>
        </Space>
      </Card>

      <Card className="glass-card">
        <Table<QrActivationResponse>
          rowKey="id"
          dataSource={filteredItems}
          loading={query.isFetching}
          rowSelection={{
            selectedRowKeys,
            onChange: (keys) => setSelectedRowKeys(keys),
            preserveSelectedRowKeys: true,
          }}
          columns={[
            { title: t('code'), dataIndex: 'code' },
            {
              title: t('name'),
              render: (_, record) => (
                <Space direction="vertical" size={0}>
                  <Typography.Text strong>{record.title}</Typography.Text>
                  <Typography.Text type="secondary">{record.stopAddress || '--'}</Typography.Text>
                </Space>
              ),
            },
            {
              title: t('qr_stop_zone'),
              render: (_, record) => <Tag color="blue">{record.stopZone}</Tag>,
            },
            {
              title: 'POI',
              render: (_, record) => (
                <Space direction="vertical" size={0}>
                  <Typography.Text>{record.poiName}</Typography.Text>
                  <Typography.Text type="secondary">{record.poiAddress}</Typography.Text>
                </Space>
              ),
            },
            { title: t('sort_order'), dataIndex: 'sortOrder', width: 90 },
            { title: t('scan_mode'), dataIndex: 'scanMode' },
            { title: t('updated_at'), render: (_, record) => formatDateTime(record.updatedAt) },
            { title: t('status'), render: (_, record) => <Tag color={record.isActive ? 'green' : 'red'}>{record.isActive ? t('active') : t('inactive')}</Tag> },
            {
              title: t('actions'),
              render: (_, record) => (
                <Space>
                  <Button icon={<Download size={16} />} onClick={() => setPreviewingQr(record)}>
                    {t('qr_preview_image')}
                  </Button>
                  <Button onClick={() => { setEditing(record); setOpen(true); }}>{t('edit')}</Button>
                  <ConfirmDeleteButton onConfirm={() => deleteMutation.mutate(record.id)} loading={deleteMutation.isPending} />
                </Space>
              ),
            },
          ]}
          expandable={{
            expandedRowRender: (record) => (
              <Space align="start" size="large" wrap>
                <QRCode value={record.deepLink} type="svg" />
                <Space direction="vertical">
                  <Typography.Text strong>{record.deepLink}</Typography.Text>
                  <Typography.Text>{record.stopZone} · {record.title}</Typography.Text>
                  <Typography.Text type="secondary">{record.stopAddress || record.poiAddress || '--'}</Typography.Text>
                  <Typography.Text type="secondary">{record.description || '--'}</Typography.Text>
                </Space>
              </Space>
            ),
          }}
        />
      </Card>

      {selectedItems.length > 0 ? (
        <Card className="glass-card">
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <div>
              <Typography.Title level={4} style={{ marginBottom: 4 }}>
                {t('qr_preview_title')}
              </Typography.Title>
              <Typography.Text type="secondary">
                {selectedItems.length} QR · {groupedSelectedItems.length} khu vuc
              </Typography.Text>
            </div>
            <div
              ref={previewRef}
              className="qr-print-sheet"
              style={{ display: 'flex', flexDirection: 'column', gap: 24 }}
            >
              {groupedSelectedItems.map((group) => (
                <div key={group.zone} className="qr-zone-block" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                  <div style={{ borderBottom: '1px solid #dbe2ea', paddingBottom: 8 }}>
                    <Typography.Title level={5} style={{ margin: 0 }}>{group.zone}</Typography.Title>
                    <Typography.Text type="secondary">{group.records.length} QR</Typography.Text>
                  </div>
                  <div className="qr-zone-grid" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
                    {group.records.map((record) => (
                      <div
                        key={record.id}
                        className="qr-print-card"
                        style={{
                          border: '1px solid #dbe2ea',
                          borderRadius: 18,
                          padding: 16,
                          display: 'grid',
                          gridTemplateColumns: '120px 1fr',
                          gap: 16,
                          alignItems: 'center',
                          background: '#fff',
                        }}
                      >
                        <div style={{ display: 'flex', justifyContent: 'center' }}>
                          <QRCode value={record.deepLink} type="svg" size={112} />
                        </div>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                          <div style={{ fontSize: 12, fontWeight: 700, textTransform: 'uppercase', color: '#2563eb' }}>{record.stopZone}</div>
                          <div style={{ fontSize: 18, fontWeight: 700, lineHeight: 1.2 }}>{record.title}</div>
                          <div style={{ fontSize: 13, color: '#475569' }}>{record.stopAddress || record.poiAddress || '--'}</div>
                          <div style={{ fontSize: 14 }}><strong>POI:</strong> {record.poiName}</div>
                          <div style={{ fontSize: 12, color: '#64748b' }}>{record.code} · {record.scanMode}</div>
                          <div style={{ fontSize: 11, color: '#64748b', wordBreak: 'break-all' }}>{record.deepLink}</div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </Space>
        </Card>
      ) : null}

      <Modal open={open} onCancel={() => { setOpen(false); setEditing(null); }} footer={null} title={editing ? t('qr_activation_edit') : t('qr_activation_create')} destroyOnClose>
        <QrActivationForm
          initialValues={editing}
          loading={createMutation.isPending || updateMutation.isPending}
          onSubmit={async (values) => {
            if (editing) {
              await updateMutation.mutateAsync({ id: editing.id, payload: values });
              return;
            }

            await createMutation.mutateAsync(values);
          }}
        />
      </Modal>

      <Modal
        open={Boolean(previewingQr)}
        onCancel={() => setPreviewingQr(null)}
        title={t('qr_preview_image_title')}
        destroyOnClose
        footer={(
          <Space>
            <Button onClick={() => setPreviewingQr(null)}>Dong</Button>
            <Button type="primary" icon={<Download size={16} />} onClick={() => void handleDownloadQrImage()}>
              {t('qr_download_image')}
            </Button>
          </Space>
        )}
      >
        {previewingQr ? (
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <div
              ref={qrImageRef}
              style={{
                display: 'flex',
                justifyContent: 'center',
                padding: 20,
                borderRadius: 18,
                background: 'linear-gradient(180deg, rgba(248,250,252,1) 0%, rgba(241,245,249,1) 100%)',
              }}
            >
              <QRCode value={previewingQr.deepLink} type="svg" size={240} />
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              <Typography.Text strong>{previewingQr.title}</Typography.Text>
              <Typography.Text>{previewingQr.stopZone} | {previewingQr.code}</Typography.Text>
              <Typography.Text type="secondary">{previewingQr.stopAddress || previewingQr.poiAddress || '--'}</Typography.Text>
              <Typography.Text type="secondary" style={{ wordBreak: 'break-all' }}>{previewingQr.deepLink}</Typography.Text>
            </div>
          </Space>
        ) : null}
      </Modal>
    </PageContainer>
  );
}
