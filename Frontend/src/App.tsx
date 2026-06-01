import { RouterProvider } from 'react-router-dom';
import { ConfigProvider, theme as antdTheme } from 'antd';

import { HelmetProvider } from 'react-helmet-async';
import { StylesContext } from './context';
import routes from './routes/routes.tsx';
import { useSelector } from 'react-redux';
import { RootState } from './redux/store';
import './App.css';

// Brand palette aligned with the legacy TradenetAdmin (Stisla) look:
// Material Blue (#2196f3) primary so the new app keeps the familiar vibe.
export const COLOR = {
  50: '#e3f2fd',
  100: '#bbdefb',
  200: '#90caf9',
  300: '#64b5f6',
  400: '#42a5f5',
  500: '#2196f3',
  600: '#1e88e5',
  700: '#1976d2',
  800: '#1565c0',
  900: '#0d47a1',
  borderColor: '#E7EAF3B2',
};

function App() {
  const { mytheme } = useSelector((state: RootState) => state.theme);

  return (
    <HelmetProvider>
      <ConfigProvider
        theme={{
          token: {
            colorPrimary: COLOR['500'],
            borderRadius: 6,
            fontFamily: "Poppins, Lato, -apple-system, 'Segoe UI', sans-serif",
          },
          components: {
            Breadcrumb: {
              // linkColor: 'rgba(0,0,0,.8)',
              // itemColor: 'rgba(0,0,0,.8)',
            },
            Button: {
              colorLink: COLOR['500'],
              colorLinkActive: COLOR['700'],
              colorLinkHover: COLOR['300'],
            },
            Calendar: {
              colorBgContainer: 'none',
            },
            Card: {
              colorBorderSecondary: COLOR['borderColor'],
            },
            Carousel: {
              colorBgContainer: COLOR['800'],
              dotWidth: 8,
            },
            Rate: {
              colorFillContent: COLOR['100'],
              colorText: COLOR['600'],
            },
            Segmented: {
              colorBgLayout: COLOR['100'],
              borderRadius: 6,
              colorTextLabel: '#000000',
            },
            Table: {
              borderColor: COLOR['100'],
              colorBgContainer: 'none',
              headerBg: '#f4f6f9',
              headerColor: '#5a607f',
              headerSplitColor: 'transparent',
              cellPaddingBlock: 14,
              rowHoverBg: COLOR['50'],
            },
            Tabs: {
              colorBorderSecondary: COLOR['100'],
            },
            Timeline: {
              dotBg: 'none',
            },
            Typography: {
              colorLink: COLOR['500'],
              colorLinkActive: COLOR['700'],
              colorLinkHover: COLOR['300'],
              linkHoverDecoration: 'underline',
            },
          },
          algorithm:
            mytheme === 'dark'
              ? antdTheme.darkAlgorithm
              : antdTheme.defaultAlgorithm,
        }}
      >
        <StylesContext.Provider
          value={{
            rowProps: {
              gutter: [
                { xs: 8, sm: 16, md: 24, lg: 32 },
                { xs: 8, sm: 16, md: 24, lg: 32 },
              ],
            },
            carouselProps: {
              autoplay: true,
              dots: true,
              dotPosition: 'bottom',
              infinite: true,
              slidesToShow: 3,
              slidesToScroll: 1,
            },
          }}
        >
          <RouterProvider router={routes} />
        </StylesContext.Provider>
      </ConfigProvider>
    </HelmetProvider>
  );
}

export default App;
