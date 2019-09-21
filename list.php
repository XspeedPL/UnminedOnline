<?php
	header('Content-Type: text/plain; charset=us-ascii');
	$dir = $_GET['dir'] ?? 'region';
	if (is_dir($dir) && $handle = opendir($dir)) {
		while (false !== ($file = readdir($handle)))
		{
			if ($file != "." && $file != ".." && strtolower(substr($file, strrpos($file, '.') + 1)) == 'mca')
			{
				echo filemtime($dir . '/' . $file) . "\t" . $file . "\n";
			}
		}
		closedir($handle);
	}
?>
