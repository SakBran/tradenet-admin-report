import { Flex, FlexProps } from 'antd';
import { Link } from 'react-router-dom';
import { CSSProperties } from 'react';

import './styles.css';

type LogoProps = {
  color: CSSProperties['color'];
  imgSize?: {
    h?: number | string;
    w?: number | string;
  };
  asLink?: boolean;
  href?: string;
  bgColor?: CSSProperties['backgroundColor'];
} & Partial<FlexProps>;

export const Logo = ({
  asLink,
  color,
  href,
  imgSize,
  bgColor,
  ...others
}: LogoProps) => {
  // Auth screens render the lockup on a dark/branded background with
  // color="white"; the official wordmark is navy, so it needs a light plate
  // there to stay legible. On light surfaces it sits on its own.
  const onDark = color === 'white';

  const brand = (
    <Flex align="center" justify="center" {...others}>
      <span
        className={`logo-mark${onDark ? ' logo-mark--ondark' : ''}`}
        style={bgColor ? { background: bgColor } : undefined}
      >
        <img
          className="logo-img"
          src="/tradenet-logo.png"
          alt="Myanmar TradeNet 2.0 — Ministry of Commerce"
          style={{ height: imgSize?.h ?? 40 }}
        />
      </span>
    </Flex>
  );

  return asLink ? (
    <Link
      to={href || '#'}
      className="logo-link"
      aria-label="Myanmar TradeNet 2.0 Report"
    >
      {brand}
    </Link>
  ) : (
    brand
  );
};
