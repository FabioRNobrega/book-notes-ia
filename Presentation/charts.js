(function () {
  const theme = {
    bg: '#3c3834',
    border: 'rgba(190,170,150,0.35)',
    text: 'rgb(232,225,218)',
    muted: 'rgb(201,192,184)',
    primary300: '#f0c5b6',
    primary400: '#d8b2a4',
    primary600: '#997e74',
    success: '#8fbc8f',
    danger: '#b96f68',
  };

  mermaid.initialize({
    startOnLoad: false,
    theme: 'base',
    fontFamily: "'Crimson Text', Georgia, serif",
    themeVariables: {
      primaryColor: '#493c38',
      primaryTextColor: theme.text,
      primaryBorderColor: theme.primary400,
      lineColor: theme.primary600,
      secondaryColor: '#67554f',
      tertiaryColor: '#2d2522',
      mainBkg: '#493c38',
      nodeBorder: theme.primary400,
      clusterBkg: 'rgba(45,37,34,0.6)',
      clusterBorder: theme.border,
      edgeLabelBackground: '#2d2522',
      fontSize: '15px',
    },
    flowchart: { curve: 'basis', htmlLabels: true },
    sequence: { actorFontFamily: "'Crimson Text', Georgia, serif" },
  });

  const renderedMermaid = new Set();

  async function renderMermaidIn(slideEl) {
    const nodes = Array.from(slideEl.querySelectorAll('.mermaid')).filter(
      (n) => !renderedMermaid.has(n)
    );
    if (nodes.length === 0) return;
    nodes.forEach((n) => renderedMermaid.add(n));
    await mermaid.run({ nodes });
  }

  const charts = new Map();

  function getChart(id, builder) {
    if (charts.has(id)) return charts.get(id).chart;
    const el = document.getElementById(id);
    if (!el) return null;
    const chart = echarts.init(el, null, { renderer: 'svg' });
    chart.setOption(builder(chart));
    charts.set(id, { chart, builder });
    return chart;
  }

  function baseOption(extra) {
    return Object.assign(
      {
        backgroundColor: 'transparent',
        textStyle: { color: theme.text, fontFamily: "'Crimson Text', Georgia, serif" },
        grid: { left: 10, right: 16, top: 36, bottom: 28, containLabel: true },
      },
      extra
    );
  }

  const builders = {
    'chart-vectorspace': () => {
      const question = [9.4, 8.6];
      const retrieved = [
        { coord: [8.7, 9.1], dist: 0.12 },
        { coord: [9.9, 7.8], dist: 0.19 },
      ];
      const otherHighlights = [
        [1.2, 6.8], [1.8, 7.4], [2.1, 6.2], [1.4, 5.9], [2.6, 7.1],
        [7.8, 1.6], [8.4, 2.2], [7.2, 1.1], [8.9, 1.4], [7.6, 2.6],
        [4.6, 4.2], [5.1, 4.8], [4.2, 3.6], [3.5, 8.3], [11.2, 4.8],
      ];

      return baseOption({
        legend: {
          data: ['Question vector', 'Highlights', 'Retrieved (closest)', 'Your question'],
          top: 0,
          right: 0,
          textStyle: { color: theme.muted, fontSize: 11 },
          itemWidth: 10,
          itemHeight: 10,
        },
        xAxis: {
          type: 'value',
          name: 'semantic dimension 1',
          nameLocation: 'middle',
          nameGap: 22,
          nameTextStyle: { color: theme.muted, fontSize: 10 },
          min: 0,
          max: 12,
          axisLine: { lineStyle: { color: theme.border } },
          axisLabel: { color: theme.muted, fontSize: 10 },
          splitLine: { lineStyle: { color: 'rgba(255,255,255,0.06)' } },
        },
        yAxis: {
          type: 'value',
          name: 'semantic dimension 2',
          nameTextStyle: { color: theme.muted, fontSize: 10 },
          min: 0,
          max: 10,
          axisLine: { lineStyle: { color: theme.border } },
          axisLabel: { color: theme.muted, fontSize: 10 },
          splitLine: { lineStyle: { color: 'rgba(255,255,255,0.06)' } },
        },
        series: [
          {
            name: 'Question vector',
            type: 'lines',
            coordinateSystem: 'cartesian2d',
            data: [{ coords: [[0, 0], question] }],
            lineStyle: { color: theme.danger, width: 3 },
            symbol: ['none', 'arrow'],
            symbolSize: [0, 14],
            z: 3,
            silent: true,
          },
          {
            name: 'Highlights',
            type: 'scatter',
            symbolSize: 11,
            itemStyle: { color: theme.primary600, opacity: 0.85 },
            data: otherHighlights,
          },
          {
            name: 'Retrieved (closest)',
            type: 'lines',
            coordinateSystem: 'cartesian2d',
            lineStyle: { color: theme.success, width: 1.5, type: 'dashed', curveness: 0 },
            data: retrieved.map((r) => ({ coords: [question, r.coord] })),
            silent: true,
            z: 1,
          },
          {
            name: 'Retrieved (closest)',
            type: 'scatter',
            symbolSize: 15,
            itemStyle: { color: theme.success },
            label: {
              show: true,
              formatter: (p) => `Δ ${retrieved[p.dataIndex].dist.toFixed(2)}`,
              position: 'right',
              color: theme.success,
              fontSize: 11,
            },
            data: retrieved.map((r) => r.coord),
            z: 2,
          },
          {
            name: 'Your question',
            type: 'scatter',
            symbol: 'diamond',
            symbolSize: 17,
            itemStyle: { color: theme.primary300 },
            label: { show: true, formatter: 'your question', position: 'top', color: theme.primary300, fontSize: 11 },
            data: [question],
            z: 2,
          },
        ],
      });
    },

    'chart-reliability': () =>
      baseOption({
        title: {
          text: 'Grounded-answer reliability (illustrative)',
          textStyle: { color: theme.muted, fontSize: 12, fontWeight: 'normal' },
          left: 0,
          top: 0,
        },
        xAxis: {
          type: 'category',
          data: ['No context', 'Keyword search', 'Embedding\nretrieval', '+ Token\ncontrol', '+ Summary'],
          axisLine: { lineStyle: { color: theme.border } },
          axisLabel: { color: theme.muted, fontSize: 11, interval: 0 },
        },
        yAxis: {
          type: 'value',
          max: 100,
          axisLine: { show: false },
          splitLine: { lineStyle: { color: 'rgba(255,255,255,0.06)' } },
          axisLabel: { color: theme.muted, fontSize: 11 },
        },
        series: [
          {
            type: 'bar',
            data: [22, 38, 72, 81, 88],
            barWidth: '52%',
            itemStyle: { color: theme.primary400, borderRadius: [3, 3, 0, 0] },
          },
        ],
      }),

    'chart-latency': () =>
      baseOption({
        title: {
          text: 'Response latency (illustrative)',
          textStyle: { color: theme.muted, fontSize: 12, fontWeight: 'normal' },
          left: 0,
          top: 0,
        },
        xAxis: {
          type: 'category',
          data: ['No context', 'Raw context', 'Selected\ncontext', 'Selected +\nsummary'],
          axisLine: { lineStyle: { color: theme.border } },
          axisLabel: { color: theme.muted, fontSize: 11, interval: 0 },
        },
        yAxis: {
          type: 'value',
          name: 'seconds',
          nameTextStyle: { color: theme.muted, fontSize: 10 },
          axisLine: { show: false },
          splitLine: { lineStyle: { color: 'rgba(255,255,255,0.06)' } },
          axisLabel: { color: theme.muted, fontSize: 11 },
        },
        series: [
          {
            type: 'line',
            data: [1.1, 4.8, 2.6, 3.4],
            smooth: true,
            symbolSize: 8,
            lineStyle: { color: theme.primary300, width: 3 },
            itemStyle: { color: theme.primary300 },
            areaStyle: { color: 'rgba(240,197,182,0.12)' },
          },
        ],
      }),

    'chart-vram-devices': () =>
      baseOption({
        grid: { left: 150, right: 70, top: 16, bottom: 26, containLabel: false },
        tooltip: {
          trigger: 'item',
          backgroundColor: theme.bg,
          borderColor: theme.border,
          textStyle: { color: theme.text, fontSize: 12 },
          formatter: (p) =>
            `${p.name}<br/>${p.value} GB GPU-addressable<br/>` +
            `<span style="opacity:.7">&#8776; ${Math.round(p.value * 1.25)}B params, Q4, illustrative</span>`,
        },
        xAxis: {
          type: 'value',
          name: 'GB GPU-addressable',
          nameTextStyle: { color: theme.muted, fontSize: 10 },
          axisLine: { lineStyle: { color: theme.border } },
          axisLabel: { color: theme.muted, fontSize: 10 },
          splitLine: { lineStyle: { color: 'rgba(255,255,255,0.06)' } },
        },
        yAxis: {
          type: 'category',
          inverse: true,
          data: [
            'Legion Go S (this machine)',
            'AMD Radeon RX 7900 XTX',
            'NVIDIA RTX 5090',
            'Mac mini (M4 Pro, max)',
            'AMD Ryzen AI Max+ 395',
            'NVIDIA DGX Spark',
          ],
          axisLine: { lineStyle: { color: theme.border } },
          axisLabel: { color: theme.muted, fontSize: 11 },
        },
        series: [
          {
            type: 'bar',
            barWidth: '55%',
            label: { show: true, position: 'right', color: theme.text, fontSize: 11, formatter: '{c} GB' },
            itemStyle: { borderRadius: [0, 3, 3, 0] },
            data: [
              { value: 15.8, itemStyle: { color: theme.primary300 } },
              { value: 24, itemStyle: { color: theme.primary600 } },
              { value: 32, itemStyle: { color: theme.primary600 } },
              { value: 64, itemStyle: { color: theme.primary600 } },
              { value: 96, itemStyle: { color: theme.primary400 } },
              { value: 128, itemStyle: { color: theme.success } },
            ],
          },
        ],
      }),

    'chart-model-sizes': (chart) => {
      const w = chart.getWidth();
      const h = chart.getHeight();
      const minDim = Math.min(w, h);
      const margin = minDim * 0.06;
      const cornerX = margin;
      const cornerY = h - margin;
      const models = [
        { name: 'qwen3.5:122b', gb: 81 },
        { name: 'qwen3.5:35b', gb: 24 },
        { name: 'qwen3.5:27b', gb: 17 },
        { name: 'qwen3.5:9b', gb: 6.6 },
        { name: 'qwen3.5:4b', gb: 3.4, ours: true },
        { name: 'qwen3.5:2b', gb: 2.7 },
        { name: 'qwen3.5:0.8b', gb: 1.0 },
      ];
      const maxSide = minDim * 0.8;
      const maxSqrt = Math.sqrt(models[0].gb);
      const elements = [];
      models.forEach((m, i) => {
        const side = maxSide * (Math.sqrt(m.gb) / maxSqrt);
        elements.push({
          type: 'rect',
          z: i,
          shape: { x: cornerX, y: cornerY - side, width: side, height: side, r: 3 },
          style: {
            fill: m.ours ? 'rgba(240,197,182,0.16)' : 'rgba(73,60,56,0.05)',
            stroke: m.ours ? theme.primary300 : theme.border,
            lineWidth: m.ours ? 2.5 : 1,
          },
          tooltip: {
            show: true,
            formatter: () =>
              `<strong>${m.name}</strong><br/>${m.gb} GB on disk` +
              (m.ours ? '<br/><span style="opacity:.75">what this app actually uses</span>' : ''),
          },
        });
      });
      return baseOption({
        tooltip: {
          trigger: 'item',
          backgroundColor: theme.bg,
          borderColor: theme.border,
          textStyle: { color: theme.text, fontSize: 12 },
        },
        title: {
          text: 'qwen3.5 family by on-disk size (to scale)',
          textStyle: { color: theme.muted, fontSize: 12, fontWeight: 'normal' },
          left: 0,
          top: 0,
        },
        graphic: { elements },
      });
    },
  };

  function initChartsIn(slideEl) {
    Array.from(slideEl.querySelectorAll('[data-echarts]')).forEach((el) => {
      const builder = builders[el.id];
      if (builder) getChart(el.id, builder);
    });
  }

  function resizeAndRebuild() {
    charts.forEach(({ chart, builder }) => {
      chart.resize();
      chart.setOption(builder(chart));
    });
  }

  window.addEventListener('slide:entered', (e) => {
    const slideEl = e.detail.slide;
    renderMermaidIn(slideEl);
    initChartsIn(slideEl);
    requestAnimationFrame(resizeAndRebuild);
  });

  window.addEventListener('resize', resizeAndRebuild);
})();
