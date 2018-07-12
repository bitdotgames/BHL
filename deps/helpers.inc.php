<?php

function is_win()
{
  return !(DIRECTORY_SEPARATOR == '/');
}

function is_linux()
{
  return PHP_OS == 'Linux';
}

function make_tmp_file_name($file_name)
{
  $meta = stream_get_meta_data(tmpfile());
  if(!isset($meta['uri']))
    throw new Exception("Could not get temp directory name");
  $tmp_dir = dirname($meta['uri']);
  $tmp_file = "$tmp_dir/$file_name";
  return $tmp_file;
}

//NOTE: crossplatform version
function scan_files_rec(array $dirs, array $only_extensions = array(), $mode = 1)
{
  $files = array();
  foreach($dirs as $dir)
  {
    if(!is_dir($dir))
      continue;

    $dir = normalize_path($dir);

    $iter_mode = $mode == 1 ? RecursiveIteratorIterator::LEAVES_ONLY : RecursiveIteratorIterator::SELF_FIRST; 
    $iter = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($dir), $iter_mode);

    foreach($iter as $filename => $cur) 
    {
      if(($mode == 1 && !$cur->isDir()) || 
         ($mode == 2 && $cur->isDir()))
      {
        if(!$only_extensions)
          $files[] = $filename;
        else
        {
          $flen = strlen($filename); 
          foreach($only_extensions as $ext)
          {
            if(substr_compare($filename, $ext, $flen-strlen($ext)) === 0)
              $files[] = $filename;
          }
        }
      }
    }
  }
  return $files;
}

function normalize_path($path, $unix=null/*null means try to guess*/)
{
  if(is_null($unix)) 
    $unix = !is_win();

  $path = str_replace('\\', '/', $path);
  $path = preg_replace('/\/+/', '/', $path);
  $parts = explode('/', $path);
  $absolutes = array();
  foreach($parts as $part) 
  {
    if('.' == $part) 
      continue;

    if('..' == $part) 
      array_pop($absolutes);
    else
      $absolutes[] = $part;
  }
  $res = implode($unix ? '/' : '\\', $absolutes);
  return $res;
}

function cli_path($path)
{
  if(is_win())
  {
    $path = '"'.normalize_path(trim($path, '"'), !is_win()).'"';
    if(strpos($path, ':') === false)
      $path = trim($path, '"');
    return $path;
  }
  else
    return '\''.normalize_path(trim($path, '\''), !is_win()).'\'';
}

function need_to_regen($file, array $deps, $fmtime_map = null)
{
  if($fmtime_map === null)
  {
    if(!is_file($file))
    {
      //echo "! $file\n";
      return true;
    }

    $fmtime = filemtime($file); 
    foreach($deps as $dep)
    {
      if(is_file($dep) && (filemtime($dep) > $fmtime))
      {
        //echo "$dep > $file\n";
        return true;
      }
    }

    return false;
  }
  else
  {
    if(!isset($fmtime_map[$file]))
      return true;
    $fmtime = $fmtime_map[$file];

    foreach($deps as $dep)
    {
      if(isset($fmtime_map[$dep]) && $fmtime_map[$dep] > $fmtime)
      {
        //echo "$dep > $file\n";
        return true;
      }
    }

    return false;
  }
}

function gen_file($settings_tpl, $settings_out, $deps = array(), $force = false, $perms = 0640)
{
  global $GAME_ROOT;

  $deps[] = $settings_tpl;
  if($force || need_to_regen($settings_out, $deps))
  {
    $txt = file_get_contents($settings_tpl);
    if($txt === false)
      throw Exception("Bad template settings file $settings_tpl");

    //replacing %(FOO)% alike entries with taskman config values
    $out = taskman_str($txt);

    if(is_file($settings_out))
    {
      $prev = file_get_contents($settings_out);
      if($prev === false)
        throw Exception("Could not read file $settings_out");
      //if contents is similar no need to write it
      if(!$force && strcmp($prev, $out) === 0)
        return;
    }

    ensure_write($settings_out, $out);
    chmod($settings_out, $perms);
  }
}

function ensure_read($file)
{
  $c = file_get_contents($file);
  if($c === false)
    throw new Exception("Could not read file '$file'");
  return $c;
}

function ensure_write($dst, $txt, $dir_perms = 0777, $flags = 0)
{
  $dir = dirname($dst);
  if(!is_dir($dir))
    mkdir($dir, $dir_perms, true);

  taskman_msg("> $dst ...\n");
  if(!file_put_contents($dst, $txt, $flags))
    throw new Exception("Could not write to '$dst'");
}

function ensure_copy($src, $dst, $dir_perms = 0777, $excludes = array())
{
  recurse_copy($src, $dst, $dir_perms, false, false, $excludes);
}

function ensure_sync($src, $dst, $dir_perms = 0777, $excludes = array())
{
  recurse_copy($src, $dst, $dir_perms, false, true, $excludes);
}

function ensure_duplicate($src, $dst, $dir_perms = 0777)
{
  recurse_copy($src, $dst, $dir_perms, true);
}

function recurse_copy($src, $dst, $dir_perms = 0777, $sys_copy = false, $mtime_check = false, $excludes = array()) 
{
  taskman_msg("copying $src => $dst ...\n");

  if(!is_file($src) && !is_dir($src))
    throw new Exception("Bad file or dir '$src'");

  foreach($excludes as $exclude_pattern)
  {
    if(preg_match("~$exclude_pattern~", $src))
      return;
  }

  if(!is_dir($src))
  {
    _ensure_copy_file($src, $dst, $sys_copy, $mtime_check);
    return;
  }

  $dir = opendir($src);
  ensure_mkdir($dst, $dir_perms);
  while(false !== ($file = readdir($dir))) 
  {
    if(($file != '.' ) && ($file != '..')) 
    {
      if(is_dir($src . '/' . $file))
        recurse_copy($src . '/' . $file, $dst . '/' . $file, $dir_perms, $sys_copy, $mtime_check, $excludes);
      else
      {
        $excluded = false;
        foreach($excludes as $exclude_pattern)
        {
          $excluded = $excluded || (bool)preg_match("~$exclude_pattern~", $src . '/' . $file);
        }

        if($excluded)
          continue;

        _ensure_copy_file($src . '/' . $file, $dst . '/' . $file, $sys_copy, $mtime_check);
      }
    }
  }
  closedir($dir);
} 

function _ensure_copy_file($src, $dst, $sys_copy = false, $mtime_check = false)
{
  if($mtime_check && file_exists($dst) && filemtime($src) <= filemtime($dst))
    return;

  if($sys_copy)
    taskman_shell_ensure("cp -a $src $dst");
  else
  { 
    ensure_mkdir(dirname($dst));
    if(!copy($src, $dst))
      throw new Exception("Could not copy '$src' to '$dst'");
  }
}

function ensure_symlink($src, $dst, $dir_perms = 0777)
{
  if(!is_file($src) && !is_dir($src))
    throw new Exception("Bad file or dir '$src'");

  $dir = dirname($dst);
  if(!is_dir($dir))
    mkdir($dir, $dir_perms, true);

  ensure_rm($dst);

  taskman_msg("symlinking $src -> $dst \n");
  if(!symlink($src, $dst))
    throw new Exception("Could not create symlink");
}

function ensure_rm($what)
{
  if(is_dir($what) && !is_link($what))
    rrmdir($what);
  else if(is_file($what) || is_link($what))
    unlink($what);
}

function ensure_mkdir($dir, $perms = 0775)
{
  if(is_dir($dir))
    return;
  
  taskman_msg("mkdir $dir\n");
  if(!mkdir($dir, $perms, true))
    throw new Exception("Could not create dir '$dir'");

  taskman_msg("chmod " . decoct($perms) . " $dir\n");
  if(!chmod($dir, $perms))
    throw new Exception("Could not chmod " . decoct($perms) . " dir '$dir'");
}

function rrmdir($dir, $remove_top_dir = true) 
{
  if(is_dir($dir))
  {
    $objects = scandir($dir);
    foreach($objects as $object) 
    {
      if($object != "." && $object != "..") 
      {
        if(filetype($dir."/".$object) == "dir") 
          rrmdir($dir."/".$object); 
        else 
          unlink($dir."/".$object);
      }
    }
  }

  if($remove_top_dir)
  {
    if(is_link($dir))
      unlink($dir);
    else if(is_dir($dir))
      rmdir($dir);
  }
} 

function fill_template($tpl, array $replaces)
{
  return str_replace(array_keys($replaces), array_values($replaces), $tpl);
}

function write_template($tpl, array $replaces, $file, $exists_check = false, $content_check = false)
{
  if($exists_check && file_exists($file))
  {
    taskman_msg("! $file\n");
    return;
  }
  $body = fill_template($tpl, $replaces);

  if($content_check && file_exists($file))
  {
    $prev = file_get_contents($file);
    if($prev === false)
      throw Exception("Could not read file $file");
    //if contents is similar no need to write it
    if(strcmp($prev, $body) === 0)
      return;
  }

  ensure_write($file, $body);
}

function hg_get_branch()
{
  exec("hg branch", $out);
  return $out[0];
}

function hg_get_tip_version($local = true)
{
  exec("hg tip", $out);
  foreach($out as $line)
  {
    if(preg_match('~^changeset:\s+(\d+):([^\s]+)~', $line, $m))
      return $local ? $m[1] : $m[2];
  }
  return null;
}

function file_put_contents_atomic($filename, $content, $mode = 0644) 
{ 
  $temp = tempnam(dirname($filename), 'atomic'); 
  if(!($f = @fopen($temp, 'wb'))) 
    throw new Exception("Error writing temporary file '$temp'"); 

  fwrite($f, $content); 
  fclose($f); 

  if(!@rename($temp, $filename)) 
  { 
    @unlink($filename); 
    @rename($temp, $filename); 
  } 
  chmod($filename, $mode); 
} 
